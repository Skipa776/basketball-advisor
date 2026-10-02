"""Fit the empirical-Bayes priors behind HOT/COLD labels and write params plus goldens.

Usage: uv run python heat_prior.py --season 2026 [--out out/heat_prior.json]

Everything is scale-free (relative to the player's baseline mean) so one fit serves every
league's scoring. The C# HeatClassifier applies the same formulas; goldens/heat_prior.json
holds cases both sides must agree on to 1e-6.
"""

from __future__ import annotations

import argparse
import json
import math
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from scipy.stats import norm

from db import connect

RECENT_GAMES = 5
MIN_BASELINE_GAMES = 10
MAX_BASELINE_GAMES = 30
MINUTE_BIN_WIDTH = 6
MIN_BASELINE_MINUTES = 15.0  # labels and fit cover rotation players only
EFFECT_FLOOR_POINTS = 2.0  # measured 2025-26: ~10% of rotation players labelled at P >= 0.75
EFFECT_FLOOR_SD = 0.0  # a 0.4 SD floor labelled under 1%

# ESPN default points (LeagueCatalog.EspnDefaultPointsRules): fitting only, never served.
ESPN_POINTS = {"PTS": 1, "FG3M": 1, "FGM": 2, "FGA": -1, "FTM": 1, "FTA": -1,
               "REB": 1, "AST": 2, "STL": 4, "BLK": 4, "TOV": -2}

GAMES_SQL = """
select pgs.player_id, s.played_on, pgs.statistics
from player_game_stat pgs
join (
    select distinct on (game_id) id, played_on
    from box_score_snapshot
    where season_end_year = %s and phase = 'RegularSeason'
    order by game_id, fetched_at desc
) s on s.id = pgs.snapshot_id
where pgs.did_play and pgs.statistics is not null
order by pgs.player_id, s.played_on
"""


def load_games(season: int) -> dict[str, list[tuple[float, float]]]:
    """(fantasy points, minutes) per appearance, in date order, keyed by player."""
    games: dict[str, list[tuple[float, float]]] = defaultdict(list)
    with connect() as conn, conn.cursor() as cur:
        cur.execute(GAMES_SQL, (season,))
        for player_id, _played_on, stats in cur:
            points = sum(stats.get(key, 0) * weight for key, weight in ESPN_POINTS.items())
            games[str(player_id)].append((float(points), float(stats.get("MIN", 0))))
    return games


def windows(games: dict[str, list[tuple[float, float]]]):
    """Every disjoint (baseline, recent) pair the classifier would see during the season."""
    for series in games.values():
        for end in range(RECENT_GAMES + MIN_BASELINE_GAMES, len(series) + 1):
            recent = series[end - RECENT_GAMES:end]
            baseline = series[max(0, end - RECENT_GAMES - MAX_BASELINE_GAMES):end - RECENT_GAMES]
            yield baseline, recent


def minute_bin(minutes: float) -> int:
    return int(minutes // MINUTE_BIN_WIDTH)


def fit(games: dict[str, list[tuple[float, float]]]) -> tuple[dict, dict]:
    rows = []
    for baseline, recent in windows(games):
        points = np.array([p for p, _ in baseline])
        mean_b = points.mean()
        minutes_b = float(np.mean([m for _, m in baseline]))
        if mean_b <= 0 or minutes_b < MIN_BASELINE_MINUTES:
            continue
        rows.append({
            "n_b": len(points),
            "mean_b": mean_b,
            "var_b": points.var(ddof=1),
            "delta": np.mean([p for p, _ in recent]) - mean_b,
            "bin": minute_bin(minutes_b),
        })

    # Pool coefficient of variation per minutes bin: sqrt(mean(var / mean^2)).
    by_bin: dict[int, list[float]] = defaultdict(list)
    for row in rows:
        by_bin[row["bin"]].append(row["var_b"] / row["mean_b"] ** 2)
    cv_bins = [{"minutesFrom": b * MINUTE_BIN_WIDTH, "minutesTo": (b + 1) * MINUTE_BIN_WIDTH,
                "cv": math.sqrt(float(np.mean(values)))}
               for b, values in sorted(by_bin.items()) if len(values) >= 50]
    cv_of = {b["minutesFrom"] // MINUTE_BIN_WIDTH: b["cv"] for b in cv_bins}

    # nu by moments: var/pool ratios spread from sampling (2/(n-1)) and from true volatility
    # differences (2/(nu-4) under a scaled inverse-chi-square prior on each player's variance).
    usable = [r for r in rows if r["bin"] in cv_of]
    ratios = np.array([r["var_b"] / (cv_of[r["bin"]] * r["mean_b"]) ** 2 for r in usable])
    observed = ratios.var() / ratios.mean() ** 2
    sampling = float(np.mean([2 / (r["n_b"] - 1) for r in usable]))
    excess = (1 + observed) / (1 + sampling) - 1
    nu = float(np.clip(4 + 2 / excess, 5, 200)) if excess > 0 else 200.0

    # tau (relative) by moments: E[d^2] = tau^2 + E[se^2].
    rel_delta, rel_se2 = [], []
    for r in usable:
        pool_var = (cv_of[r["bin"]] * r["mean_b"]) ** 2
        shrunk = ((r["n_b"] - 1) * r["var_b"] + nu * pool_var) / (r["n_b"] - 1 + nu)
        rel_delta.append(r["delta"] / r["mean_b"])
        rel_se2.append(shrunk * (1 / RECENT_GAMES + 1 / r["n_b"]) / r["mean_b"] ** 2)
    tau_rel = math.sqrt(max(float(np.mean(np.square(rel_delta)) - np.mean(rel_se2)), 1e-4))

    params = {
        "recentGames": RECENT_GAMES, "minBaselineGames": MIN_BASELINE_GAMES,
        "minBaselineMinutes": MIN_BASELINE_MINUTES,
        "maxBaselineGames": MAX_BASELINE_GAMES, "nu": nu, "tauRelative": tau_rel,
        "effectFloorPoints": EFFECT_FLOOR_POINTS, "effectFloorSd": EFFECT_FLOOR_SD,
        "cvByMinutes": cv_bins,
    }
    metrics = {"windows": len(usable), "players": len(games), "observedRatioCv2": float(observed),
               "samplingCv2": sampling}
    return params, metrics


def posterior(params: dict, baseline: list[float], baseline_minutes: float, recent: list[float]) -> dict:
    """The served formula; HeatClassifier.cs must match it."""
    n_b, n_r = len(baseline), len(recent)
    mean_b = float(np.mean(baseline))
    var_b = float(np.var(baseline, ddof=1))
    scale = abs(mean_b)
    bins = params["cvByMinutes"]
    cv = next((b["cv"] for b in bins if b["minutesFrom"] <= baseline_minutes < b["minutesTo"]), bins[-1]["cv"])
    nu = params["nu"]
    shrunk = ((n_b - 1) * var_b + nu * (cv * scale) ** 2) / (n_b - 1 + nu)
    se2 = shrunk * (1 / n_r + 1 / n_b)
    tau2 = (params["tauRelative"] * scale) ** 2
    delta = float(np.mean(recent)) - mean_b
    mean = tau2 / (tau2 + se2) * delta
    sd = math.sqrt(tau2 * se2 / (tau2 + se2))
    floor = max(params["effectFloorPoints"], params["effectFloorSd"] * math.sqrt(shrunk))
    return {"shiftMean": mean, "shiftSd": sd, "effectFloor": floor,
            "pHot": float(1 - norm.cdf((floor - mean) / sd)),
            "pCold": float(norm.cdf((-floor - mean) / sd))}


ENTER, EXIT = 0.75, 0.60
NULL_SHUFFLES = 200
REPLAY_WINDOWS = 10


def label_at_end(params: dict, series: list[tuple[float, float]]) -> str | None:
    """Replays the last windows with enter/exit hysteresis, as HeatClassifier.Classify does."""
    label = None
    first = max(RECENT_GAMES + MIN_BASELINE_GAMES, len(series) - REPLAY_WINDOWS + 1)
    for end in range(first, len(series) + 1):
        recent = series[end - RECENT_GAMES:end]
        baseline = series[max(0, end - RECENT_GAMES - MAX_BASELINE_GAMES):end - RECENT_GAMES]
        points = [p for p, _ in baseline]
        minutes = float(np.mean([m for _, m in baseline]))
        if minutes < MIN_BASELINE_MINUTES or np.mean(points) <= 0:
            label = None
            continue
        x = posterior(params, points, minutes, [p for p, _ in recent])
        if label == "hot" and x["pHot"] >= EXIT or label == "cold" and x["pCold"] >= EXIT:
            continue
        label = "hot" if x["pHot"] >= ENTER else "cold" if x["pCold"] >= ENTER else None
    return label


def false_label_rate(params: dict, games: dict[str, list[tuple[float, float]]]) -> dict:
    """Shuffling each season's game order removes every real trend; labels that remain are chance."""
    rng = np.random.default_rng(7)
    eligible = [s for s in games.values() if len(s) >= RECENT_GAMES + MIN_BASELINE_GAMES]
    observed = sum(label_at_end(params, s) is not None for s in eligible)
    shuffled = 0
    for _ in range(NULL_SHUFFLES):
        for series in eligible:
            order = rng.permutation(len(series))
            shuffled += label_at_end(params, [series[i] for i in order]) is not None
    return {"players": len(eligible), "observedLabels": observed,
            "nullLabelRate": shuffled / (NULL_SHUFFLES * len(eligible)),
            "observedLabelRate": observed / len(eligible), "nullShuffles": NULL_SHUFFLES}


def goldens(params: dict) -> dict:
    rng = np.random.default_rng(20261001)
    cases = []
    for level, shift, minutes in [(30, 0, 32), (30, 15, 32), (25, -12, 28), (12, 6, 16), (45, -4, 36)]:
        baseline = [round(float(x), 1) for x in rng.normal(level, 0.3 * level, 20)]
        recent = [round(float(x), 1) for x in rng.normal(level + shift, 0.3 * level, RECENT_GAMES)]
        cases.append({"baseline": baseline, "baselineMinutes": minutes, "recent": recent,
                      "expected": posterior(params, baseline, minutes, recent)})
    return {"model": "heat-prior", "parameters": params, "cases": cases}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--season", type=int, required=True)
    parser.add_argument("--out", default="out/heat_prior.json")
    args = parser.parse_args()
    games = load_games(args.season)
    params, metrics = fit(games)
    metrics["falseLabels"] = false_label_rate(params, games)
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    record = {"modelName": "heat-prior", "version": f"heat-prior-{fitted_at:%Y%m%d%H%M}",
              "fittedAt": fitted_at.isoformat(), "trainSeasonEndYears": [args.season],
              "parameters": params, "metrics": metrics,
              "cardMarkdown": "Empirical-Bayes priors for HOT/COLD labels: volatility shrinkage nu, "
                              "relative shift spread tau and pool CV by minutes, fitted by moments."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/heat_prior.json").write_text(json.dumps(goldens(params), indent=2) + "\n")
    print(json.dumps({"nu": params["nu"], "tauRelative": params["tauRelative"], **metrics}, indent=2))
    # ponytail: the null is rerun at each refit, not nightly; refit after big data changes.


if __name__ == "__main__":
    main()
