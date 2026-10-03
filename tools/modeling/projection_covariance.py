"""Fit the per-game stat covariance behind projection intervals and write params plus goldens.

Usage: uv run python projection_covariance.py --target 2022 2023 2024 2025 [--out out/covariance.json]
(reads out/projection.json, out/minutes.json and out/availability.json from the other fits)

For a player with predicted per-minute rates r, minutes m and expected games g, the per-game
stats x_s = r_s * m have covariance

    Sigma = k^2 * [ (m^2 + Var m) * D R D + r r' Var m ]

D = diag of each rate's predictive SD, sqrt(phi_s r_s / (m g) + (tau_s r_s)^2) from the rate
fit; Var m = sigma^2 / g + tau_m^2 from the minutes fit; R is the correlation of standardized
rate residuals within the position group (FGM moves with FGA, FTM with FTA). The minutes
term is what correlates points, rebounds and assists. k is one scale, set on the training
seasons so 80% intervals for ESPN fantasy points per game cover 80%. Any linear scoring c
then has variance c' Sigma c. The C# StatCovariance mirrors this; goldens/covariance.json
holds cases both sides must agree on to 1e-6.
"""

from __future__ import annotations

import argparse
import json
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

from availability_model import load as load_games, posterior as games_posterior
from minutes_model import load_seasons, predict as predict_minutes
from projection_model import GROUPS, RATE_STATS, history, load_lines, predict_rate, target_age

MIN_TARGET_GAMES = 20  # as the backtest
Z80 = 1.2815515655446004
# ESPN default points (LeagueCatalog.EspnDefaultPointsRules) on the modelled stats: PTS = 2 FGM + FG3M + FTM
# and REB = OREB + DREB are folded in, so FGM carries 2 (FGM) + 2 (PTS) and so on.
ESPN = {"OREB": 1, "DREB": 1, "AST": 2, "STL": 4, "BLK": 4, "TOV": -2, "FGM": 4, "FGA": -1,
        "FG3M": 2, "FG3A": 0, "FTM": 2, "FTA": -1, "PF": 0}


def components(rates_p: dict, minutes_p: dict, games_p: dict, group: str, rates: np.ndarray,
               minutes: float, games: float) -> tuple[np.ndarray, float]:
    """Per-stat rate SDs and the minutes variance for one player."""
    stats = rates_p["stats"]
    season_minutes = max(minutes * games, 1.0)
    sd = np.array([np.sqrt(stats[s]["phi"] * r / season_minutes + (stats[s]["tau"] * r) ** 2)
                   for s, r in zip(RATE_STATS, rates)])
    var_m = minutes_p["sigma"] ** 2 / max(games, 1.0) + minutes_p["tau"] ** 2
    return sd, var_m


def covariance(sd: np.ndarray, var_m: float, rates: np.ndarray, minutes: float, corr: np.ndarray, k: float) -> np.ndarray:
    return k**2 * ((minutes**2 + var_m) * np.outer(sd, sd) * corr + np.outer(rates, rates) * var_m)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=int, nargs="+", required=True)
    parser.add_argument("--out", default="out/covariance.json")
    parser.add_argument("--models", default="out", help="directory holding the rate, minutes and availability fits")
    args = parser.parse_args()
    rates_rec, minutes_rec, games_rec = (json.loads((Path(args.models) / f"{name}.json").read_text())
                                         for name in ("projection", "minutes", "availability"))
    rates_p, minutes_p, games_p = rates_rec["parameters"], minutes_rec["parameters"], games_rec["parameters"]
    lines, groups = load_lines()
    mins = load_seasons()
    games_seasons, length = load_games()

    rows = []
    for player, seasons in lines.items():
        for target in args.target:
            actual = mins.get(player, {}).get(target)
            past = history(seasons, target)
            if actual is None or actual["games"] < MIN_TARGET_GAMES or not past:
                continue
            age = target_age(past)
            group = groups[player]
            rates = np.array([predict_rate(rates_p, s, group, age, past) for s in RATE_STATS])
            min_past = [{**mins[player][target - lag], "lag": lag} for lag in (1, 2, 3)
                        if target - lag in mins[player] and mins[player][target - lag]["games"] >= minutes_p["minHistoryGames"]]
            minutes = predict_minutes(minutes_p, age, min_past) if min_past else minutes_p["mu"]
            g_past = [{"lag": lag, "games": min(games_seasons[player][target - lag]["games"], length[target - lag]),
                       "seasonGames": length[target - lag], "mpg": games_seasons[player][target - lag]["mpg"]}
                      for lag in (1, 2, 3) if target - lag in games_seasons.get(player, {})]
            alpha, beta = games_posterior(games_p, age, g_past)
            games = 82 * alpha / (alpha + beta)
            sd, var_m = components(rates_p, minutes_p, games_p, group, rates, minutes, games)
            actual_rates = np.array([seasons[target].get(s, 0.0) / seasons[target]["MIN"] for s in RATE_STATS])
            actual_x = np.array([seasons[target].get(s, 0.0) / actual["games"] for s in RATE_STATS])
            rows.append({"group": group, "z": (actual_rates - rates) / sd, "sd": sd, "varM": var_m,
                         "rates": rates, "minutes": minutes, "fpActual": float(actual_x @ np.array(list(ESPN.values()))),
                         "fpMean": float(rates @ np.array(list(ESPN.values())) * minutes)})

    pooled = np.corrcoef(np.array([r["z"] for r in rows]).T)
    corr = {}
    for group in GROUPS:
        z = np.array([r["z"] for r in rows if r["group"] == group])
        corr[group] = np.corrcoef(z.T) if len(z) >= 100 else pooled  # U is small: pooled
    c = np.array(list(ESPN.values()))
    raw = np.array([(r["fpActual"] - r["fpMean"]) / np.sqrt(c @ covariance(r["sd"], r["varM"], r["rates"], r["minutes"], corr[r["group"]], 1.0) @ c)
                    for r in rows])
    k = float(np.quantile(np.abs(raw), 0.8) / Z80)
    coverage = float(np.mean(np.abs(raw / k) <= Z80))

    params = {"stats": RATE_STATS, "scale": k,
              "correlation": {g: [[float(v) for v in row] for row in m] for g, m in corr.items()}}
    metrics = {"rows": len(rows), "groupRows": {g: sum(r["group"] == g for r in rows) for g in GROUPS},
               "unscaledZSd": float(np.std(raw)), "coverage80Train": coverage,
               "rateVersion": rates_rec["version"], "minutesVersion": minutes_rec["version"],
               "availabilityVersion": games_rec["version"]}
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    record = {"modelName": "projection-covariance", "version": f"projection-covariance-{fitted_at:%Y%m%d%H%M}",
              "fittedAt": fitted_at.isoformat(), "trainSeasonEndYears": args.target,
              "parameters": params, "metrics": metrics,
              "cardMarkdown": "Per-game stat covariance: rate residual correlation by position group, the "
                              "minutes variance, and one coverage scale; moments from the training seasons."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")

    cases = []
    for group, minutes, games, var_m in [("G", 34.0, 70.0, None), ("B", 22.0, 45.0, None), ("U", 12.0, 30.0, None)]:
        rates = np.array([rates_p["stats"][s]["mu"][group] for s in RATE_STATS])
        sd, var_m = components(rates_p, minutes_p, games_p, group, rates, minutes, games)
        sigma = covariance(sd, var_m, rates, minutes, np.array(params["correlation"][group]), k)
        cases.append({"group": group, "rates": dict(zip(RATE_STATS, map(float, rates))), "minutes": minutes,
                      "games": games, "expected": {"espnVariance": float(c @ sigma @ c),
                                                   "fgmFga": float(sigma[RATE_STATS.index("FGM"), RATE_STATS.index("FGA")]),
                                                   "astVariance": float(sigma[RATE_STATS.index("AST"), RATE_STATS.index("AST")])}})
    golden = {"model": "projection-covariance", "parameters": params,
              "rateParameters": rates_p, "minutesParameters": minutes_p, "cases": cases}
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/covariance.json").write_text(json.dumps(golden, indent=2) + "\n")
    print(json.dumps({**metrics, "scale": k}, indent=1))


if __name__ == "__main__":
    main()
