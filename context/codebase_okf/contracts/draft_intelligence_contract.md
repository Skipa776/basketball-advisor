---
type: contract
title: Draft Intelligence Contract
description: Survival probability, the opportunity-cost term that replaces MarketValue, tier detection, and category-aware draft value.
tags: [contract, draft, math]
source_paths: [src/FantasyBasketball.Domain/Draft, src/FantasyBasketball.Application/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests/Draft]
depends_on: [draft_value_contract.md, category_value_contract.md]
status: planned
last_updated: 2026-09-26
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Survival probability replaces MarketValue rather than being added alongside it.
  - Tier boundaries are data-derived, not a hardcoded value gap.
  - A category league gets a category-aware board, not a points board with a banner.
---

> **Superseded in part, 2026-10-03.** M2's draft_simulation_contract
> deleted `MarketValue` and urgency and reads opportunity cost and survival from
> simulated drafts instead of adding an `OpportunityCost` term. Tiers and
> category-aware draft value below remain planned.


# Responsibility

Owns the design doc's phase-5 draft intelligence: *will he still be there next
time, and does waiting actually cost me anything?*

# This supersedes the MVP's `MarketValue` term

> **Status note — 2026-09-26.** [draft_value_contract](draft_value_contract.md)
> now carries an *urgency* addend — `max(0, VAR) × (1 − P(survives to next
> pick))` with its own heuristic σ floor of 2 and 20%-of-ADP fallback, weighted
> 0.5 and still added alongside `MarketValue`. That is a display-and-nudge term,
> not this contract's `OpportunityCost`: it does not subtract the best
> alternative at the next pick, has no positional runs, and keeps its own σ
> constants. When this epic lands, urgency is deleted, `OpportunityCost`
> replaces `MarketValue`, and the sigma floor/rate below become the only σ
> source.

[draft_value_contract](draft_value_contract.md) uses `(ADP − currentPick) ×
valuePerPick` as a proxy for "you can wait." That proxy answers the wrong
question — it measures how far past his ADP a player has fallen, not what taking
someone else instead would cost. Replace it:

```text
OpportunityCost = P(taken before my next pick) × (playerValue − bestAlternativeAtNextPick)

MarketValue  →  OpportunityCost                 # a replacement, not an addition
```

`bestAlternativeAtNextPick` is the expected best available player at the user's
eligible slot at their next turn, using the same survival model over the pool.

Reading it: a player certain to be gone whose drop-off to the next-best option is
large has high opportunity cost — take him now. A player likely to survive, or
one whose alternative is nearly as good, has low opportunity cost — wait.
`MarketValue` could not express the second half of that.

**Landing this epic edits [draft_value_contract](draft_value_contract.md) in the
same commit**, replacing the `MarketValue` row and its `W_MARKET` weight with
`OpportunityCost` and `W_OPPCOST`. Two live definitions of the same term is
exactly the drift this bundle exists to prevent.

# Survival probability

A player's realized draft position `D` is modelled as `Normal(ADP, σ)`:

```text
P(survives to pick n) = P(D ≥ n) = 1 − Φ( (n − ADP) / σ )
```

`σ` comes from the ADP source when it publishes a standard deviation. Otherwise:

```text
σ = max(SIGMA_FLOOR, SIGMA_RATE × ADP)      SIGMA_FLOOR = 3.0, SIGMA_RATE = 0.25
```

Uncertainty grows with ADP on purpose — the first three picks of a draft are
near-deterministic and the 90th is not.

**Positional runs** shift the effective pick number rather than the
distribution. If the last `RUN_WINDOW` (6) picks contain `RUN_THRESHOLD` (3) or
more players whose primary position matches this player's, use `n + RUN_SHIFT`
(2) in place of `n`. A run means his position is being drafted faster than the
board average, and shifting the pick is the cheapest honest way to say so.

`Φ` is the shared `NormalDistribution` from
[category_value_contract](category_value_contract.md) — one implementation, one
test.

# Tier detection

Tiers are value cliffs, so derive the cliff threshold from the board rather than
picking a number:

```text
gaps      = [ value[i] − value[i+1] for i in top TIER_SCAN_DEPTH (100) of the pool ]
threshold = mean(gaps) + TIER_SIGMA (1.5) × stdDev(gaps)
```

A boundary falls wherever an adjacent gap `≥ threshold`. Tiers are recomputed
with the board, every pick — a tier that existed at pick 12 may not exist at
pick 30.

`Tier(Rank, TopValue, BottomValue, Size)`. "Major tier drop after this player" in
a recommendation means the next gap is a boundary, and it becomes a `Scarcity`
evidence item.

# Category-aware draft value

For category leagues, `ValueAboveReplacement` is replaced by category marginal
value from [category_value_contract](category_value_contract.md):

```text
CategoryVAR = CategoryValue(player, currentRoster)
            − CategoryValue(replacementPlayer, currentRoster)
```

Every other term (`PositionalScarcity`, `RosterFit`, `OpportunityCost`, risk)
carries over unchanged, with values in z-units rather than fantasy points.
**Units never mix**: a category league's board is entirely in z-units, a points
league's entirely in points. Mixing them is a bug that produces plausible
nonsense.

The MVP's category-league banner and fallback ordering are deleted when this
lands.

# Constants

| Constant | Value |
|---|---|
| `SIGMA_FLOOR` | `3.0` |
| `SIGMA_RATE` | `0.25` |
| `RUN_WINDOW` | `6` |
| `RUN_THRESHOLD` | `3` |
| `RUN_SHIFT` | `2` |
| `TIER_SCAN_DEPTH` | `100` |
| `TIER_SIGMA` | `1.5` |
| `W_OPPCOST` | `1.0` |

All bound from options; all candidates for
[backtest_contract](backtest_contract.md) calibration.

# Invariants

- **`OpportunityCost` replaces `MarketValue`; both never appear in `Total`.**
  *Check: [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md)
  row X-01 asserts `Total` equals the five addends with `OpportunityCost` among
  them and no `MarketValue` term.*
- **`P(survive)` is monotone decreasing in `n`** and lands in `[0, 1]`. *Check: row X-02.*
- **A player at his exact ADP with the pick equal to ADP has `P ≈ 0.5`.**
  *Check: row X-03.*
- **Missing ADP means no survival estimate** — `OpportunityCost = 0` plus a
  `Market` evidence item recording the absence. Never an imputed ADP. *Check: row X-04.*
- **A positional run raises opportunity cost** for players at that position and
  leaves others unchanged. *Check: row X-05.*
- **Tier boundaries move as the pool drains.** *Check: row X-06.*
- **Category boards are entirely in z-units**; no fantasy-point term leaks in.
  *Check: row X-07 asserts every `DraftValue` component for a category league is
  z-denominated.*
- **Full-pool survival plus tiering stays inside the R7 latency target.**
  *Check: the latency assertion.*

# Change procedure

This contract and [draft_value_contract](draft_value_contract.md) move together —
the term replacement is not complete until both say the same thing.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md),
rows X-01 through X-07.
