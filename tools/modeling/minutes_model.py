"""Fit the minutes-per-game model behind season projections and write params plus goldens.

Usage: uv run python minutes_model.py --target 2022 2023 2024 2025 [--out out/minutes.json]

A player's next-season minutes per game come from up to three prior seasons, each weighted
by games played and a fitted recency weight, shrunk toward the league mean for short
seasons, then shifted by last season's role and by age:

    G       = sum_k w_j g_k                      (weighted games; j counts back from the newest season played)
    H       = sum_k w_j g_k mpg_k / G            (weighted history)
    minutes = clamp((G H + kappa mu) / (G + kappa) + delta_role + beta * (age - AGE_CENTER), 0, MAX)

The recency weight counts from the newest season the player actually played, so a player who
sat out last season is not also discounted as if his own history were stale; the "none" role
shift still carries what missing that season tells us. (A fitted discount for injury-short
seasons, g_k (g_k / N_k)^rho, was tried: rho came out at -0.23 and the 2025-26 holdout did not
move, so it was dropped.)

Roles come from last season's minutes per game (bench < 18 <= rotation < 28 <= starter; no
last season = "none"). The role shift is the role prior: across 2021-22..2024-25, bench
players who stay in the league gain minutes and rotation players lose some, even after a
full season of games, so it is not sampling noise. (A smooth pull toward the mean was tried
first and could not fit the bench gain.) The observed minutes per game are
Normal(minutes, sigma^2 / games + tau^2). The C# MinutesModel applies the same formula
with posterior means; goldens/minutes.json holds cases both sides must agree on to 1e-6.
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
MAX_MINUTES = 42.0
MIN_TARGET_GAMES = 20  # the backtest scores players with 20+ games, so the fit does too
MIN_HISTORY_GAMES = 5
ROLES = ["bench", "rotation", "starter", "none"]
ROTATION_FROM = 18.0
STARTER_FROM = 28.0

SQL = """
select player_id, season_end_year, games_played, minutes_per_game, age
from season_stat_line where source = 'basketball-reference'
"""


def load_seasons() -> dict[str, dict[int, dict]]:
    seasons: dict[str, dict[int, dict]] = defaultdict(dict)
    with connect() as conn, conn.cursor() as cur:
        cur.execute(SQL)
        for player_id, season, games, mpg, age in cur:
            seasons[str(player_id)][season] = {"games": games, "mpg": float(mpg), "age": age}
    return seasons


def recency(past: list[dict]) -> list[int]:
    """Each line's recency index: 0 for the newest season played, then 1, 2."""
    newest = min(line["lag"] for line in past)
    return [line["lag"] - newest for line in past]


def role(past: list[dict]) -> str:
    last = next((line for line in past if line["lag"] == 1), None)
    if last is None:
        return "none"
    return "starter" if last["mpg"] >= STARTER_FROM else "rotation" if last["mpg"] >= ROTATION_FROM else "bench"


def training_rows(seasons, targets: list[int]) -> list[dict]:
    rows = []
    for lines in seasons.values():
        for target in targets:
            actual = lines.get(target)
            past = [{**lines[target - lag], "lag": lag} for lag in range(1, HISTORY_SEASONS + 1)
                    if target - lag in lines and lines[target - lag]["games"] >= MIN_HISTORY_GAMES]
            if actual is None or actual["games"] < MIN_TARGET_GAMES or not past:
                continue
            age = next((line["age"] + line["lag"] for line in past if line["age"] is not None), None)
            rows.append({"role": role(past), "age": age, "games": actual["games"],
                         "mpg": actual["mpg"], "past": past})
    return rows


def fit(rows: list[dict], draws: int) -> tuple[dict, dict]:
    n = len(rows)
    g = np.zeros((n, HISTORY_SEASONS))
    x = np.zeros((n, HISTORY_SEASONS))  # games * minutes per game = season minutes
    for i, row in enumerate(rows):
        for line, j in zip(row["past"], recency(row["past"])):
            g[i, j] = line["games"]
            x[i, j] = line["games"] * line["mpg"]
    role_index = np.array([ROLES.index(r["role"]) for r in rows])
    age = np.array([0.0 if r["age"] is None else r["age"] - AGE_CENTER for r in rows])
    games = np.array([r["games"] for r in rows], dtype=float)
    observed = np.array([r["mpg"] for r in rows])
    # A fitted mu traded off against the role shifts (kappa mu / (G + kappa) is near constant),
    # which made the sampler diverge; the training rows' mean pins it instead.
    mu = float(observed.mean())

    with pm.Model():
        w_old = pm.Beta("w_old", 2, 2, shape=HISTORY_SEASONS - 1)
        w = pm.math.concatenate([np.ones(1), w_old])
        kappa = pm.LogNormal("kappa", np.log(10), 1.0)  # sampling shrinkage, in games
        delta = pm.Normal("delta", 0.0, 3.0, shape=len(ROLES))
        beta = pm.Normal("beta", 0, 0.5)  # minutes per year of age
        sigma = pm.HalfNormal("sigma", 20.0)
        tau = pm.HalfNormal("tau", 5.0)
        weighted_games = (g * w).sum(axis=1)
        history = (x * w).sum(axis=1) / weighted_games
        # ponytail: no clamp inside the likelihood (its kink caused divergences); predictions
        # stay inside [0, 42] on the training rows, and predict() clamps.
        minutes = (weighted_games * history + kappa * mu) / (weighted_games + kappa) + delta[role_index] + beta * age
        pm.Normal("obs", minutes, pm.math.sqrt(sigma**2 / games + tau**2), observed=observed)
        trace = pm.sample(draws, tune=draws, chains=4, random_seed=11, target_accept=0.95, progressbar=False)

    post = trace.posterior
    mean = lambda name: post[name].mean(dim=("chain", "draw")).values
    params = {"ageCenter": AGE_CENTER, "maxMinutes": MAX_MINUTES, "minHistoryGames": MIN_HISTORY_GAMES,
              "weights": [1.0, *map(float, mean("w_old"))], "rotationFrom": ROTATION_FROM,
              "starterFrom": STARTER_FROM, "mu": mu, "kappa": float(mean("kappa")),
              "delta": dict(zip(ROLES, map(float, mean("delta")))), "beta": float(mean("beta")),
              "sigma": float(mean("sigma")), "tau": float(mean("tau"))}
    rhat = max(float(np.max(v)) for v in pm.stats.rhat(trace).data_vars.values())
    return params, {"rows": n, "agedRows": int(sum(r["age"] is not None for r in rows)), "maxRhat": rhat}


def predict(params: dict, age: int | None, past: list[dict]) -> float:
    """The formula the C# MinutesModel mirrors; goldens are computed with it."""
    w = params["weights"]
    v = [w[j] for j in recency(past)]
    weighted_games = sum(vk * line["games"] for vk, line in zip(v, past))
    history = sum(vk * line["games"] * line["mpg"] for vk, line in zip(v, past)) / weighted_games
    centered = 0 if age is None else age - params["ageCenter"]
    shrunk = (weighted_games * history + params["kappa"] * params["mu"]) / (weighted_games + params["kappa"])
    minutes = shrunk + params["delta"][role(past)] + params["beta"] * centered
    return min(max(minutes, 0.0), params["maxMinutes"])


def goldens(params: dict) -> dict:
    cases = [
        (22, [{"lag": 1, "games": 70, "mpg": 24.5}, {"lag": 2, "games": 40, "mpg": 15.2}]),
        (34, [{"lag": 1, "games": 55, "mpg": 33.1}, {"lag": 2, "games": 71, "mpg": 34.8},
              {"lag": 3, "games": 76, "mpg": 35.6}]),
        (None, [{"lag": 2, "games": 12, "mpg": 9.4}]),
        (25, [{"lag": 1, "games": 8, "mpg": 11.0}]),
        (20, [{"lag": 1, "games": 82, "mpg": 41.9}]),
        (31, [{"lag": 1, "games": 36, "mpg": 29.0}, {"lag": 2, "games": 67, "mpg": 34.0}]),
        (26, [{"lag": 2, "games": 73, "mpg": 33.9}, {"lag": 3, "games": 69, "mpg": 32.0}]),
    ]
    return {"model": "projection-minutes", "parameters": params,
            "cases": [{"age": age, "history": past, "expected": predict(params, age, past)} for age, past in cases]}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=int, nargs="+", required=True,
                        help="season(s) whose minutes are predicted; never the backtest holdout")
    parser.add_argument("--out", default="out/minutes.json")
    parser.add_argument("--draws", type=int, default=1000)
    args = parser.parse_args()
    rows = training_rows(load_seasons(), args.target)
    params, metrics = fit(rows, args.draws)
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    record = {"modelName": "projection-minutes", "version": f"projection-minutes-{fitted_at:%Y%m%d%H%M}",
              "fittedAt": fitted_at.isoformat(), "trainSeasonEndYears": args.target,
              "parameters": params, "metrics": metrics,
              "cardMarkdown": "Minutes per game: games-weighted three-season history, recency counted from the newest season played, shrunk toward the "
                              "league mean, plus a last-season role shift and a linear age term; NUTS posterior means."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/minutes.json").write_text(json.dumps(goldens(params), indent=2) + "\n")
    print(json.dumps({**metrics, **{k: v for k, v in params.items() if k in ("weights", "mu", "kappa", "delta", "beta", "sigma", "tau")}}, indent=1))


if __name__ == "__main__":
    main()
