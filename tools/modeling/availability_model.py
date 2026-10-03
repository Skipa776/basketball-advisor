"""Fit the games-played (availability) model behind season projections and write params plus goldens.

Usage: uv run python availability_model.py --target 2022 2023 2024 2025 [--out out/availability.json]

Games played next season ~ BetaBinomial(season games, alpha, beta). The population prior on a
player's availability p is Beta(phi * m, phi * (1 - m)) with
logit m = a + b * (age - AGE_CENTER) + c * (mpg - MPG_CENTER) / 10, where mpg is the most
recent season's minutes per game (fringe players log few games for reasons other than injury);
each prior season adds its games played and games missed as discounted pseudo-games
(a power prior: every weight, the last season's included, is fitted, since a season's
games are not independent trials — injuries come in runs):

    alpha = phi * m       + sum_k w_k g_k
    beta  = phi * (1 - m) + sum_k w_k (N_k - g_k)

N_k is that season's length (the most games any player logged, capped at 82, so the
shortened 2019-20 and 2020-21 seasons count missed games against their own length).
Players who missed a whole season have no line, so whole-season absences are not in the
fit; the model is of games played given the player suits up at all. The C# AvailabilityModel
applies the same formula with posterior means; goldens/availability.json holds cases both
sides must agree on to 1e-6.
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

HISTORY_SEASONS = 3
AGE_CENTER = 27
FULL_SEASON = 82
MPG_CENTER = 20

SQL = """
select player_id, season_end_year, games_played, age, minutes_per_game
from season_stat_line where source = 'basketball-reference' and games_played > 0
"""


def load() -> tuple[dict[str, dict[int, dict]], dict[int, int]]:
    seasons: dict[str, dict[int, dict]] = defaultdict(dict)
    length: dict[int, int] = defaultdict(int)
    with connect() as conn, conn.cursor() as cur:
        cur.execute(SQL)
        for player_id, season, games, age, mpg in cur:
            seasons[str(player_id)][season] = {"games": games, "age": age, "mpg": float(mpg)}
            length[season] = max(length[season], min(games, FULL_SEASON))
    return seasons, dict(length)


def training_rows(seasons, length, targets: list[int]) -> list[dict]:
    rows = []
    for lines in seasons.values():
        for target in targets:
            actual = lines.get(target)
            past = [{"lag": lag, "games": min(lines[target - lag]["games"], length[target - lag]),
                     "seasonGames": length[target - lag], "age": lines[target - lag]["age"],
                     "mpg": lines[target - lag]["mpg"]}
                    for lag in range(1, HISTORY_SEASONS + 1) if target - lag in lines]
            if actual is None or not past:
                continue
            age = next((line["age"] + line["lag"] for line in past if line["age"] is not None), None)
            rows.append({"age": age, "games": min(actual["games"], length[target]),
                         "seasonGames": length[target], "past": past})
    return rows


def fit(rows: list[dict], draws: int) -> tuple[dict, dict]:
    n = len(rows)
    played = np.zeros((n, HISTORY_SEASONS))
    missed = np.zeros((n, HISTORY_SEASONS))
    for i, row in enumerate(rows):
        for line in row["past"]:
            played[i, line["lag"] - 1] = line["games"]
            missed[i, line["lag"] - 1] = line["seasonGames"] - line["games"]
    age = np.array([0.0 if r["age"] is None else r["age"] - AGE_CENTER for r in rows])
    mpg = np.array([(r["past"][0]["mpg"] - MPG_CENTER) / 10 for r in rows])  # past is newest first

    with pm.Model():
        w = pm.Beta("w", 1, 4, shape=HISTORY_SEASONS)
        a = pm.Normal("a", 1.5, 1.0)
        b = pm.Normal("b", 0.0, 0.1)
        c = pm.Normal("c", 0.0, 1.0)
        phi = pm.LogNormal("phi", np.log(10), 1.0)
        m = pm.math.sigmoid(a + b * age + c * mpg)
        alpha = phi * m + (played * w).sum(axis=1)
        beta = phi * (1 - m) + (missed * w).sum(axis=1)
        pm.BetaBinomial("obs", alpha=alpha, beta=beta, n=np.array([r["seasonGames"] for r in rows]),
                        observed=np.array([r["games"] for r in rows]))
        trace = pm.sample(draws, tune=draws, chains=4, random_seed=13, target_accept=0.9, progressbar=False)

    post = trace.posterior
    mean = lambda name: post[name].mean(dim=("chain", "draw")).values
    params = {"ageCenter": AGE_CENTER, "mpgCenter": MPG_CENTER, "fullSeason": FULL_SEASON,
              "weights": list(map(float, mean("w"))), "a": float(mean("a")),
              "b": float(mean("b")), "c": float(mean("c")), "phi": float(mean("phi"))}
    rhat = max(float(np.max(v)) for v in pm.stats.rhat(trace).data_vars.values())
    return params, {"rows": n, "maxRhat": rhat}


def posterior(params: dict, age: int | None, past: list[dict]) -> tuple[float, float]:
    """(alpha, beta) for next season; the formula the C# AvailabilityModel mirrors."""
    w = params["weights"]
    centered = 0 if age is None else age - params["ageCenter"]
    minutes = (past[0]["mpg"] - params["mpgCenter"]) / 10
    m = 1 / (1 + np.exp(-(params["a"] + params["b"] * centered + params["c"] * minutes)))
    alpha = params["phi"] * m + sum(w[line["lag"] - 1] * line["games"] for line in past)
    beta = params["phi"] * (1 - m) + sum(w[line["lag"] - 1] * (line["seasonGames"] - line["games"]) for line in past)
    return float(alpha), float(beta)


def coverage(params: dict, rows: list[dict]) -> dict:
    """Share of target seasons inside the central 80% of the predictive Beta-Binomial."""
    from scipy.stats import betabinom
    inside, errors = 0, []
    for row in rows:
        alpha, beta = posterior(params, row["age"], row["past"])
        dist = betabinom(row["seasonGames"], alpha, beta)
        inside += dist.ppf(0.1) <= row["games"] <= dist.ppf(0.9)
        errors.append(abs(dist.mean() - row["games"]))
    return {"coverage80": inside / len(rows), "maeGames": float(np.mean(errors))}


def goldens(params: dict) -> dict:
    cases = [
        (24, [{"lag": 1, "games": 74, "seasonGames": 82, "mpg": 33.5},
              {"lag": 2, "games": 70, "seasonGames": 82, "mpg": 30.1}]),
        (31, [{"lag": 1, "games": 22, "seasonGames": 82, "mpg": 28.0},
              {"lag": 2, "games": 61, "seasonGames": 82, "mpg": 31.2},
              {"lag": 3, "games": 40, "seasonGames": 72, "mpg": 30.4}]),
        (None, [{"lag": 2, "games": 50, "seasonGames": 82, "mpg": 12.3}]),
        (20, [{"lag": 1, "games": 82, "seasonGames": 82, "mpg": 8.0}]),
    ]
    out = []
    for age, past in cases:
        alpha, beta = posterior(params, age, past)
        out.append({"age": age, "history": past, "expected": {"alpha": alpha, "beta": beta}})
    return {"model": "projection-availability", "parameters": params, "cases": out}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=int, nargs="+", required=True,
                        help="season(s) whose games played are predicted; never the backtest holdout")
    parser.add_argument("--out", default="out/availability.json")
    parser.add_argument("--draws", type=int, default=1000)
    args = parser.parse_args()
    seasons, length = load()
    rows = training_rows(seasons, length, args.target)
    params, metrics = fit(rows, args.draws)
    metrics.update(coverage(params, rows))
    metrics["seasonGames"] = {str(k): v for k, v in sorted(length.items())}
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    record = {"modelName": "projection-availability", "version": f"projection-availability-{fitted_at:%Y%m%d%H%M}",
              "fittedAt": fitted_at.isoformat(), "trainSeasonEndYears": args.target,
              "parameters": params, "metrics": metrics,
              "cardMarkdown": "Games played: Beta-Binomial with an age-dependent population prior updated by "
                              "recency-weighted games played and missed; NUTS posterior means."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/availability.json").write_text(json.dumps(goldens(params), indent=2) + "\n")
    print(json.dumps({**metrics, **params}, indent=1))


if __name__ == "__main__":
    main()
