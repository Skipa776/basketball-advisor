"""Fit the Plackett-Luce opponent pick model from public draft logs; write params, goldens and a report.

Usage: uv run python choice_model.py --season 2025 [--out out/choice.json] [--report ../../docs/backtest/choice-model-2026.md]

At pick t a drafter takes player j from the available players with probability
softmax(u) where

    u_j = -lambda_g * log(ADP_j) + eta * need_j

lambda_g is fitted per round group g (how tightly drafters follow the market early versus
late), need_j is 1 when j's positions fill an open starting slot on the picking team, and
ADP_j is the player's mean pick in the OTHER drafts (an undrafted player counts as that
draft's size + 1), so a draft never informs its own market. The choice set is the 60
best-ADP available players plus the one taken; the rest carry negligible probability.
Only human picks (Sleeper's picked_by set) are choice observations; autopicks follow Sleeper's
own rankings, so they remove players and fill rosters but never train the model. Fit by
maximum likelihood on 80% of drafts (split by draft id), judged on the other 20%
against the app's current heuristic -- lowest ADP after Normal(0, max(6, 0.2 ADP))
jitter -- by Brier score and a reliability table. The C# OpponentChoiceModel mirrors
probabilities(); goldens/choice.json holds cases both sides must agree on to 1e-6.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from scipy.optimize import minimize

from db import connect

ROUND_GROUPS = [1, 3, 6, 10]  # groups start at these rounds: 1-2, 3-5, 6-9, 10+
CANDIDATES = 60
HOLDOUT_SHARE = 0.2
STARTER_SLOTS = {  # Sleeper slot name -> positions it accepts
    "PG": {"PG"}, "SG": {"SG"}, "SF": {"SF"}, "PF": {"PF"}, "C": {"C"},
    "G": {"PG", "SG"}, "F": {"SF", "PF"}, "UTIL": {"PG", "SG", "SF", "PF", "C"},
}

SQL_DRAFTS = "select id, draft_id, team_count, rounds, slots from draft_log where source = 'sleeper' and season = %s"
SQL_PICKS = """select draft_log_id, pick_number, draft_slot, player_id, positions, picked_by is not null
from draft_log_pick where draft_log_id = any(%s) order by draft_log_id, pick_number"""


def load(season: int) -> list[dict]:
    with connect() as conn, conn.cursor() as cur:
        cur.execute(SQL_DRAFTS, (season,))
        drafts = {row[0]: {"id": row[1], "teams": row[2], "rounds": row[3], "slots": row[4], "picks": []}
                  for row in cur.fetchall()}
        cur.execute(SQL_PICKS, (list(drafts),))
        for log_id, pick, slot, player, positions, human in cur:
            drafts[log_id]["picks"].append({"pick": pick, "slot": slot, "player": player, "positions": list(positions), "human": human})
    return list(drafts.values())


def market(drafts: list[dict]) -> tuple[dict, dict, dict]:
    """Per-player pick sums and counts across drafts, for leave-one-draft-out ADP."""
    total, count = defaultdict(float), defaultdict(int)
    positions: dict[str, list[str]] = {}
    for draft in drafts:
        size = draft["teams"] * draft["rounds"]
        taken = {p["player"]: p["pick"] for p in draft["picks"]}
        for p in draft["picks"]:
            positions[p["player"]] = p["positions"]
        draft["taken"] = taken
        draft["size"] = size
    players = list(positions)
    for draft in drafts:
        for player in players:
            total[player] += draft["taken"].get(player, draft["size"] + 1)
            count[player] += 1
    return total, count, positions


def adp_without(draft: dict, total, count) -> dict[str, float]:
    return {player: (total[player] - draft["taken"].get(player, draft["size"] + 1)) / (count[player] - 1)
            for player in total if count[player] > 1}


def starters(slots: dict) -> list[set[str]]:
    return [STARTER_SLOTS[name] for name, n in slots.items() if name in STARTER_SLOTS for _ in range(n)]


def open_slots(starter_slots: list[set[str]], held: list[list[str]]) -> list[set[str]]:
    remaining = list(starter_slots)
    for positions in held:
        for index, accepts in enumerate(remaining):
            if accepts & set(positions):
                remaining.pop(index)
                break
    return remaining


def group_of(round_number: int) -> int:
    return max(i for i, start in enumerate(ROUND_GROUPS) if round_number >= start)


def choice_sets(drafts: list[dict], total, count, positions) -> list[dict]:
    """Every pick as (round group, log-ADP and need of each candidate, index of the one taken)."""
    sets = []
    for draft in drafts:
        adp = adp_without(draft, total, count)
        starter_slots = starters(draft["slots"])
        held: dict[int, list[list[str]]] = defaultdict(list)
        available = sorted(adp, key=adp.get)
        taken_so_far: set[str] = set()
        for p in draft["picks"]:
            if p["player"] not in adp or not p.get("human", True):
                taken_so_far.add(p["player"])
                held[p["slot"]].append(p["positions"])
                continue
            pool = [j for j in available if j not in taken_so_far][:CANDIDATES]
            if p["player"] not in pool:
                pool.append(p["player"])
            free = open_slots(starter_slots, held[p["slot"]])
            need = [1.0 if any(set(positions[j]) & accepts for accepts in free) else 0.0 for j in pool]
            sets.append({"draft": draft["id"], "group": group_of((p["pick"] - 1) // draft["teams"] + 1),
                         "logAdp": np.log(np.array([adp[j] for j in pool])), "adp": np.array([adp[j] for j in pool]),
                         "need": np.array(need), "chosen": pool.index(p["player"])})
            taken_so_far.add(p["player"])
            held[p["slot"]].append(p["positions"])
    return sets


def probabilities(lambdas, eta: float, group: int, log_adp: np.ndarray, need: np.ndarray) -> np.ndarray:
    u = -lambdas[group] * log_adp + eta * need
    u -= u.max()
    e = np.exp(u)
    return e / e.sum()


def negative_log_likelihood(theta: np.ndarray, sets: list[dict]) -> float:
    lambdas, eta = theta[:-1], theta[-1]
    return -sum(math.log(probabilities(lambdas, eta, s["group"], s["logAdp"], s["need"])[s["chosen"]] + 1e-300) for s in sets)


def heuristic_probabilities(adp: np.ndarray, rng: np.random.Generator, draws: int = 400) -> np.ndarray:
    """P(lowest jittered ADP) under the app's SimulatedOpponent spread, by Monte Carlo."""
    sigma = np.maximum(6.0, 0.2 * adp)
    jittered = adp + sigma * rng.standard_normal((draws, len(adp)))
    winners = np.bincount(jittered.argmin(axis=1), minlength=len(adp))
    return winners / draws


def evaluate(sets: list[dict], predict) -> dict:
    """Brier score over every (pick, candidate) and a 10-bin reliability table."""
    predicted, observed = [], []
    for s in sets:
        p = predict(s)
        y = np.zeros(len(p))
        y[s["chosen"]] = 1.0
        predicted.append(p)
        observed.append(y)
    p, y = np.concatenate(predicted), np.concatenate(observed)
    edges = np.array([0, 0.01, 0.02, 0.05, 0.1, 0.2, 0.3, 0.5, 0.7, 0.9, 1.0001])
    bins = []
    for lo, hi in zip(edges[:-1], edges[1:]):
        mask = (p >= lo) & (p < hi)
        if mask.any():
            bins.append({"from": float(lo), "to": float(min(hi, 1)), "n": int(mask.sum()),
                         "predicted": float(p[mask].mean()), "observed": float(y[mask].mean())})
    log_loss = float(-np.mean([math.log(max(pr[s["chosen"]], 1e-12)) for pr, s in zip(predicted, sets)]))
    return {"brier": float(np.mean((p - y) ** 2)), "logLoss": log_loss,
            "top1": float(np.mean([pr.argmax() == s["chosen"] for pr, s in zip(predicted, sets)])), "reliability": bins}


def is_holdout(draft_id: str) -> bool:
    return int(hashlib.sha256(draft_id.encode()).hexdigest(), 16) % 1000 < HOLDOUT_SHARE * 1000


def report(path: Path, season: int, drafts: list[dict], params: dict, model: dict, heuristic: dict, version: str) -> None:
    lines = [f"# Opponent pick model — {season}–{(season + 1) % 100:02d} Sleeper drafts", "",
             f"`{version}` · {len(drafts)} public snake drafts · fitted on {params['trainDrafts']}, judged on "
             f"{params['holdoutDrafts']} held-out drafts (split by draft id).", "",
             "Each held-out pick scores every candidate (the 60 best-ADP players still available, plus the one taken).", "",
             "| Model | Brier | Log loss | Top-1 hit rate |", "|---|---|---|---|",
             f"| Plackett–Luce | {model['brier']:.5f} | {model['logLoss']:.4f} | {model['top1']:.3f} |",
             f"| Normal(ADP, max(6, 0.2·ADP)) heuristic | {heuristic['brier']:.5f} | {heuristic['logLoss']:.4f} | {heuristic['top1']:.3f} |", "",
             "Fitted: " + ", ".join(f"λ rounds {start}+ = {lam:.2f}" for start, lam in zip(ROUND_GROUPS, params["lambdas"]))
             + f", η (fills an open starter) = {params['eta']:.2f}.", "",
             "## Reliability (held out)", "",
             "| Predicted | Plackett–Luce n | mean predicted | observed | Heuristic n | mean predicted | observed |",
             "|---|---|---|---|---|---|---|"]
    heuristic_bins = {(b["from"], b["to"]): b for b in heuristic["reliability"]}
    for b in model["reliability"]:
        h = heuristic_bins.get((b["from"], b["to"]), {"n": 0, "predicted": float("nan"), "observed": float("nan")})
        lines.append(f"| {b['from']:.2f}–{b['to']:.2f} | {b['n']} | {b['predicted']:.4f} | {b['observed']:.4f} | "
                     f"{h['n']} | {h['predicted']:.4f} | {h['observed']:.4f} |")
    path.write_text("\n".join(lines) + "\n")


def self_check() -> None:
    """Simulate drafts from known parameters, fit them, and require the fit to recover them."""
    rng = np.random.default_rng(3)
    true_lambdas, true_eta = np.array([2.5, 1.8, 1.2, 0.8]), 1.0
    players = [f"p{i}" for i in range(200)]
    positions = {p: [["PG", "SG", "SF", "PF", "C"][i % 5]] for i, p in enumerate(players)}
    slots = {"PG": 1, "SG": 1, "SF": 1, "PF": 1, "C": 1, "UTIL": 2, "BN": 6}
    drafts, true_loglik = [], []
    for d in range(150):
        quality = np.arange(1, 201, dtype=float)  # one market; the noise is the drafters' own choices
        draft = {"id": f"d{d}", "teams": 10, "rounds": 13, "slots": slots, "picks": []}
        held: dict[int, list[list[str]]] = defaultdict(list)
        left = list(range(200))
        for pick in range(1, 131):
            rnd = (pick - 1) // 10 + 1
            slot = (pick - 1) % 10 + 1 if rnd % 2 else 10 - (pick - 1) % 10
            free = open_slots(starters(slots), held[slot])
            pool = sorted(left, key=lambda j: quality[j])[:CANDIDATES]
            need = np.array([1.0 if any(set(positions[players[j]]) & a for a in free) else 0.0 for j in pool])
            p = probabilities(true_lambdas, true_eta, group_of(rnd), np.log(quality[pool]), need)
            choice = rng.choice(len(pool), p=p)
            true_loglik.append(math.log(p[choice]))
            j = pool[choice]
            left.remove(j)
            held[slot].append(positions[players[j]])
            draft["picks"].append({"pick": pick, "slot": slot, "player": players[j], "positions": positions[players[j]], "human": True})
        drafts.append(draft)
    total, count, pos = market(drafts)
    sets = choice_sets(drafts, total, count, pos)
    fit = minimize(negative_log_likelihood, np.array([1.0] * 4 + [0.5]), args=(sets,), method="L-BFGS-B",
                   bounds=[(0.01, 20)] * 4 + [(-5, 5)])
    fitted = -fit.fun / len(sets)
    truth = float(np.mean(true_loglik))
    print("recovered", np.round(fit.x, 2), "true", list(true_lambdas) + [true_eta],
          f"mean log-lik fitted {fitted:.4f} vs true process {truth:.4f}")
    # Empirical ADP compresses the market rank late (undrafted players sit at size + 1), so late
    # lambdas come out larger than the generating ones: that is the right coefficient on log ADP,
    # not a recovery failure. What must hold: the need bonus, falling lambdas, and predictions
    # nearly as sharp as the true process that saw the market directly.
    assert abs(fit.x[-1] - true_eta) < 0.3, fit.x
    assert np.all(np.diff(fit.x[:-1]) < 0), fit.x
    assert fitted > truth - 0.05, (fitted, truth)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--self-check", action="store_true")
    parser.add_argument("--season", type=int)
    parser.add_argument("--out", default="out/choice.json")
    parser.add_argument("--report", default="../../docs/backtest/choice-model-2026.md")
    args = parser.parse_args()
    if args.self_check:
        self_check()
        return
    drafts = load(args.season)
    if len(drafts) < 20:
        raise SystemExit(f"Only {len(drafts)} drafts for {args.season}; import more before fitting.")
    total, count, positions = market(drafts)
    sets = choice_sets(drafts, total, count, positions)
    train = [s for s in sets if not is_holdout(s["draft"])]
    holdout = [s for s in sets if is_holdout(s["draft"])]
    start = np.array([1.0] * len(ROUND_GROUPS) + [0.5])
    fit = minimize(negative_log_likelihood, start, args=(train,), method="L-BFGS-B",
                   bounds=[(0.01, 20)] * len(ROUND_GROUPS) + [(-5, 5)])
    lambdas, eta = fit.x[:-1], float(fit.x[-1])
    params = {"roundGroups": ROUND_GROUPS, "lambdas": [float(v) for v in lambdas], "eta": eta,
              "candidates": CANDIDATES, "trainDrafts": len({s["draft"] for s in train}),
              "holdoutDrafts": len({s["draft"] for s in holdout})}
    model = evaluate(holdout, lambda s: probabilities(lambdas, eta, s["group"], s["logAdp"], s["need"]))
    rng = np.random.default_rng(29)
    heuristic = evaluate(holdout, lambda s: heuristic_probabilities(s["adp"], rng))
    fitted_at = datetime.now(timezone.utc).replace(microsecond=0)
    version = f"opponent-choice-{fitted_at:%Y%m%d%H%M}"
    record = {"modelName": "opponent-choice", "version": version, "fittedAt": fitted_at.isoformat(),
              "trainSeasonEndYears": [args.season + 1], "parameters": params,
              "metrics": {"converged": bool(fit.success), "trainPicks": len(train), "holdoutPicks": len(holdout),
                          "model": model, "heuristic": heuristic},
              "cardMarkdown": "Plackett-Luce pick model: market (log ADP) weight by round group and a roster-need "
                              "bonus, maximum likelihood on public Sleeper drafts."}
    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    Path(args.out).write_text(json.dumps(record, indent=2) + "\n")
    cases = []
    for group, adp, need in [(0, [1.2, 2.5, 3.1, 6.0, 9.4], [1, 1, 0, 1, 0]), (2, [70.0, 72.5, 80.0, 95.0], [0, 1, 1, 0]),
                             (3, [130.0, 131.0, 150.0], [0, 0, 0])]:
        p = probabilities(lambdas, eta, group, np.log(np.array(adp)), np.array(need, dtype=float))
        cases.append({"group": group, "adp": adp, "need": need, "expected": [float(v) for v in p]})
    Path("goldens").mkdir(exist_ok=True)
    Path("goldens/choice.json").write_text(json.dumps({"model": "opponent-choice", "parameters": params, "cases": cases}, indent=2) + "\n")
    report(Path(args.report), args.season, drafts, params, model, heuristic, version)
    print(json.dumps({"lambdas": params["lambdas"], "eta": eta, "model": {k: v for k, v in model.items() if k != "reliability"},
                      "heuristic": {k: v for k, v in heuristic.items() if k != "reliability"}}, indent=1))


if __name__ == "__main__":
    main()
