"""Fit hierarchical per-minute rate priors for season projections and write params plus goldens.

Usage: uv run python projection_model.py --target 2023 2024 2025 [--out out/projection.json]

For each stat, a player's next-season rate is predicted from up to three prior seasons:

    shrunk = (sum_k w_k m_k r_k + kappa * mu_g) / (sum_k w_k m_k + kappa)
    rate   = shrunk * exp(alpha + beta * (age - AGE_CENTER))

w_1 = 1 and w_2, w_3 are fitted recency weights, m_k are minutes, mu_g is the position
group's prior rate, and kappa is that prior's strength in minutes. The observed rate is
Normal(rate, phi * rate / minutes + (tau * rate)^2): Poisson-like sampling noise plus a
true year-to-year change. REB and PTS are not modelled; they are sums of modelled stats.
The C# HierarchicalProjector applies the same formula with posterior means;
goldens/projection.json holds cases both sides must agree on to 1e-6.
"""

from __future__ import annotations

import argparse
import json
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import pymc as pm

from db import connect

RATE_STATS = ["OREB", "DREB", "AST", "STL", "BLK", "TOV", "FGM", "FGA",
              "FG3M", "FG3A", "FTM", "FTA", "PF"]
GROUPS = ["G", "W", "B", "U"]  # guards, wings, bigs, unknown position
GROUP_OF = {"PG": "G", "SG": "G", "SF": "W", "PF": "B", "C": "B"}
HISTORY_SEASONS = 3
AGE_CENTER = 27
MIN_TARGET_MINUTES = 500  # a season this short is too noisy to score a forecast against
MIN_HISTORY_MINUTES = 100  # shorter seasons are dropped from a player's history

SQL = """
select l.player_id, l.season_end_year, l.totals, l.age, p.positions
from season_stat_line l join player p on p.id = l.player_id
where l.source = 'basketball-reference'
"""


def load_lines() -> tuple[dict[str, dict[int, dict]], dict[str, str]]:
    """Season totals (plus age) keyed by player then season, and each player's group."""
    lines: dict[str, dict[int, dict]] = defaultdict(dict)
    groups: dict[str, str] = {}
    with connect() as conn, conn.cursor() as cur:
        cur.execute(SQL)
        for player_id, season, totals, age, positions in cur:
            key = str(player_id)
            lines[key][season] = {**{k: float(v) for k, v in totals.items()}, "age": age}
            groups[key] = GROUP_OF.get(positions[0], "U") if positions else "U"
    return lines, groups


def history(seasons: dict[int, dict], target: int) -> list[dict]:
    """Up to three prior seasons, most recent first, each tagged with its lag."""
    return [{**seasons[target - lag], "lag": lag} for lag in range(1, HISTORY_SEASONS + 1)
            if target - lag in seasons and seasons[target - lag].get("MIN", 0) >= MIN_HISTORY_MINUTES]


def target_age(past: list[dict]) -> int | None:
    """The newest known age, carried forward by its lag."""
    for line in past:
        if line["age"] is not None:
            return line["age"] + line["lag"]
    return None


def training_rows(lines, groups, targets: list[int]) -> list[dict]:
    rows = []
    for player, seasons in lines.items():
        for target in targets:
            actual = seasons.get(target)
            past = history(seasons, target)
            if actual is None or actual.get("MIN", 0) < MIN_TARGET_MINUTES or not past:
                continue
            rows.append({"group": groups[player], "age": target_age(past),
                         "minutes": actual["MIN"], "actual": actual, "past": past})
    return rows


def pooled_rates(lines) -> dict[str, float]:
    minutes = sum(s.get("MIN", 0) for p in lines.values() for s in p.values())
    return {stat: sum(s.get(stat, 0) for p in lines.values() for s in p.values()) / minutes
            for stat in RATE_STATS}


def fit_stat(stat: str, rows: list[dict], pooled: float, draws: int, seed: int) -> dict:
    n = len(rows)
    m = np.zeros((n, HISTORY_SEASONS))
    x = np.zeros((n, HISTORY_SEASONS))  # minutes * rate, i.e. the stat's season total
    for i, row in enumerate(rows):
        for line in row["past"]:
            m[i, line["lag"] - 1] = line["MIN"]
            x[i, line["lag"] - 1] = line.get(stat, 0.0)
    group = np.array([GROUPS.index(r["group"]) for r in rows])
    age = np.array([0.0 if r["age"] is None else r["age"] - AGE_CENTER for r in rows])
    minutes = np.array([r["minutes"] for r in rows])
    observed = np.array([r["actual"].get(stat, 0.0) / r["minutes"] for r in rows])

    with pm.Model():
        w_old = pm.Beta("w_old", 2, 2, shape=HISTORY_SEASONS - 1)
        w = pm.math.concatenate([np.ones(1), w_old])
        kappa = pm.LogNormal("kappa", np.log(500), 1.0)
        mu = pm.LogNormal("mu", np.log(max(pooled, 1e-4)), 0.5, shape=len(GROUPS))
        alpha = pm.Normal("alpha", 0, 0.1)
        beta = pm.Normal("beta", 0, 0.03)
        phi = pm.HalfNormal("phi", 2.0)
        tau = pm.HalfNormal("tau", 0.3)
        shrunk = ((x * w).sum(axis=1) + kappa * mu[group]) / ((m * w).sum(axis=1) + kappa)
        rate = shrunk * pm.math.exp(alpha + beta * age)
        sd = pm.math.sqrt(phi * rate / minutes + (tau * rate) ** 2)
        pm.Normal("obs", rate, sd, observed=observed)
        trace = pm.sample(draws, tune=draws, chains=4, random_seed=seed, progressbar=False)

    post = trace.posterior
    mean = lambda name: post[name].mean(dim=("chain", "draw")).values
    rhat = float(max(float(np.max(v)) for v in pm.stats.rhat(trace).data_vars.values()))
    return {"weights": [1.0, *map(float, mean("w_old"))], "kappa": float(mean("kappa")),
            "mu": dict(zip(GROUPS, map(float, mean("mu")))), "alpha": float(mean("alpha")),
            "beta": float(mean("beta")), "phi": float(mean("phi")), "tau": float(mean("tau")),
            "rows": n, "maxRhat": rhat}


def predict_rate(params: dict, stat: str, group: str, age: int | None, past: list[dict]) -> float:
    """The formula the C# projector mirrors; goldens are computed with it."""
    p = params["stats"][stat]
    num = sum(p["weights"][line["lag"] - 1] * line.get(stat, 0.0) for line in past)
    den = sum(p["weights"][line["lag"] - 1] * line["MIN"] for line in past)
    shrunk = (num + p["kappa"] * p["mu"][group]) / (den + p["kappa"])
    centered = 0 if age is None else age - params["ageCenter"]
    return shrunk * float(np.exp(p["alpha"] + p["beta"] * centered))


def goldens(params: dict) -> dict:
    cases = []
    for group, age, past in [
        ("G", 24, [{"lag": 1, "MIN": 2400, "age": 23}, {"lag": 2, "MIN": 1800, "age": 22}]),
        ("B", 33, [{"lag": 1, "MIN": 2100, "age": 32}, {"lag": 2, "MIN": 2500, "age": 31},
                   {"lag": 3, "MIN": 2600, "age": 30}]),
        ("W", None, [{"lag": 2, "MIN": 300, "age": None}]),
        ("U", 21, [{"lag": 1, "MIN": 150, "age": 20}]),
    ]:
        rng = np.random.default_rng(len(cases))
        for line in past:  # whole-number totals, as Basketball-Reference publishes them
            line.update({stat: float(round(line["MIN"] * params["stats"][stat]["mu"][group]
                                           * rng.uniform(0.6, 1.4))) for stat in RATE_STATS})
        cases.append({"group": group, "age": age, "history": past,
                      "expected": {s: predict_rate(params, s, group, age, past) for s in RATE_STATS}})
    return {"model": "projection-rates", "parameters": params, "cases": cases}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=int, nargs="+", required=True,
                        help="season(s) whose rates are predicted; never the backtest holdout")
    parser.add_argument("--out", default="out/projection.json")
    parser.add_argument("--draws", type=int, default=1000)
    args = parser.parse_args()
    lines, groups = load_lines()
    rows = training_rows(lines, groups, args.target)
    pooled = pooled_rates({p: {s: l for s, l in v.items() if s < min(args.target)} for p, v in lines.items()})
    stats = {stat: fit_stat(stat, rows, pooled[stat], args.draws, seed=i) for i, stat in enumerate(RATE_STATS)}
    params = {"ageCenter": AGE_CENTER, "minHistoryMinutes": MIN_HISTORY_MINUTES, "groupOf": GROUP_OF,
              "stats": {s: {k: v for k, v in fit.items() if k not in ("rows", "maxRhat")} for s, fit in stats.items()}}
    metrics = {"rows": len(rows), "agedRows": sum(r["age"] is not None for r in rows),
               "maxRhat": max(fit["maxRhat"] for fit in stats.values())}
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    record = {"modelName": "projection-rates", "version": f"projection-rates-{fitted_at:%Y%m%d%H%M}",
              "fittedAt": fitted_at.isoformat(), "trainSeasonEndYears": args.target,
              "parameters": params, "metrics": metrics,
              "cardMarkdown": "Hierarchical per-minute rates: recency-weighted three-season history "
                              "shrunk to a position-group prior, with an age term; NUTS posterior means."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/projection.json").write_text(json.dumps(goldens(params), indent=2) + "\n")
    print(json.dumps({**metrics, **{s: {k: round(v, 4) for k, v in f.items() if isinstance(v, float)}
                                    for s, f in stats.items()}}, indent=1))


if __name__ == "__main__":
    main()
