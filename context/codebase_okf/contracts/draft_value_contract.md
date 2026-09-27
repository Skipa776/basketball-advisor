---
type: contract
title: Draft Value Contract
description: The V1 DraftValue formula, replacement level, scarcity, roster fit, market value, the weight constants, and the double-counting rules that make the terms additive.
tags: [contract, draft, scoring]
source_paths: [src/FantasyBasketball.Domain/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests/Draft]
depends_on: [projection_pipeline_contract.md, scoring_rules_catalog.md, recommendation_evidence_contract.md]
status: implemented
last_updated: 2026-09-26
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Every term is in fantasy-point units and no quantity is counted twice.
  - Weights are bound from configuration, never compiled in as literals.
  - The board re-ranks the full pool inside the R7 latency target.
---

# Responsibility

Owns what makes one available player a better *pick* than another — which is not
the same as being the better player. Terms are all in **fantasy-point units** so
the total is interpretable and the weights are dimensionless.

# Resolving two contradictions in the design doc

The design doc's illustrative formula adds `ProjectedSeasonValue` **and**
`ValueAboveReplacement` **and** `ContextAdjustment`. Those overlap: value above
replacement already contains projected value, and projected value already
contains the context adjustment, because it is computed from the
`AdjustedProjection`. Adding all three counts the same production up to three
times, which would systematically overrate high-projection players — exactly the
naive behavior the product exists to avoid.

Resolved here, and this resolution is canonical:

- **Addends** (these sum to `Total`): `ValueAboveReplacement`,
  `PositionalScarcity`, `RosterFit`, `MarketValue`, `Urgency`, and the risk
  penalty.
- **Decomposition-only fields** (displayed, never added): `ProjectedSeasonValue`,
  `ContextAdjustment`, `InjuryRisk`, `RoleRisk`.

Survival probability and tier detection are **not in V1** — they are design-doc
phase 5, listed in [post_mvp_roadmap](../tasks/post_mvp_roadmap.md). The
[urgency](#urgency) term uses a survival estimate only as a nudge; the full
survival model (opportunity cost, runs, tiering) remains E07 work.

# The formula

```text
Total = ValueAboveReplacement
      + W_SCARCITY × PositionalScarcity
      + W_FIT      × RosterFit
      + W_MARKET   × MarketValue
      − W_RISK     × RiskPenalty
      + W_URGENCY  × Urgency
```

## Value above replacement

```text
startersPerTeam  = roster slots that are not BENCH or IR
replacementRank  = TeamCount × startersPerTeam
ReplacementValue = projected season value of the player at replacementRank
                   in the CURRENTLY AVAILABLE pool
VAR              = playerSeasonValue − ReplacementValue
```

Recomputed after every pick — replacement level rises as the pool drains, which
is most of why rankings must move during a draft.

## Positional scarcity

Measures what waiting actually costs, using the picks the user really has:

```text
k          = picks until the user's next turn
scarcity(P) = max(0, bestRemaining(P).value − kthRemaining(P).value)
PositionalScarcity = max over the player's eligible positions of scarcity(P)
```

If fewer than `k` players remain at a position, the k-th value is
`ReplacementValue`.

## Roster fit

Fit measures **redundancy only** — the generic value is already in `VAR`, so fit
must not restate it:

```text
alternative = the player this pick would actually displace on the user's roster:
              an open eligible starting slot   → ReplacementValue
              all eligible slots filled        → value of the worst starter displaced

RosterFit   = ReplacementValue − alternative
```

Open slot → `0` (VAR already counted it). Position already full → negative,
scaled by how good the incumbent is. A third starting center is penalized
exactly as much as that center is redundant.

## Market value

Converts "picks earlier or later than the market" into points, so it can be
added to point-denominated terms:

```text
valuePerPick = (bestAvailableValue − ReplacementValue) / max(1, replacementRank)
MarketValue  = (currentPick − ADP) × valuePerPick
```

Positive when a player is available past their ADP (a value), negative when
taking them is a reach. With no ADP for a player, `MarketValue = 0` and an
evidence item records the absence — never a guessed ADP.

## Urgency

Prices the risk of losing the player before the user's next turn. A candidate's
realized draft position is modelled `Normal(ADP, σ)`, so the chance he survives
to `nextPick = CurrentPick + PicksUntilNextTurn` is:

```text
σ            = published ADP standard deviation, else max(6, 0.2 × ADP)   # heuristic spread
P(available) = 1 − Φ( (nextPick − ADP) / σ )
Urgency      = max(0, ValueAboveReplacement) × (1 − P(available))
```

No ADP means no survival estimate: `P(available) = null` and `Urgency = 0`,
never an imputed ADP. When `P(available) < 0.5` a `Market` evidence item states
`About N% chance he is still there at pick P` with **Supporting** polarity —
likely-gone is a reason to pick now — otherwise **Neutral** with the same
statement. Φ is the Abramowitz-Stegun 7.1.26 `erf` approximation, the same
formulation [category_value_contract](category_value_contract.md) specifies.
The σ fallback is recorded in [assumptions](../assumptions.md); the upgrade is
deriving σ from real Sleeper/platform pick distributions.
[draft_intelligence_contract](draft_intelligence_contract.md)'s `OpportunityCost`
supersedes this term when E07 lands — it is an availability nudge, not the full
survival model.

## Risk penalty

```text
RiskPenalty = playerSeasonValue × clamp((RoleRisk + InjuryRisk) / 2, 0, 1)
```

`RoleRisk` comes from the adjusted projection
([projection_pipeline_contract](projection_pipeline_contract.md)). `InjuryRisk`
in V1 comes only from active `Injury` / `MinutesRestriction` context events —
it is **variance**, not expected value, because expected games missed is already
inside `projGP`.

# Weights

Bound from configuration (`DraftWeightOptions`), never literals in code. These
are transparent starting constants chosen by judgment and **not back-tested** —
see [assumptions](../assumptions.md).

| Constant | Default |
|---|---|
| `W_SCARCITY` | `1.0` |
| `W_FIT` | `1.0` |
| `W_MARKET` | `0.5` |
| `W_RISK` | `1.0` |
| `W_URGENCY` | `0.5` |

`DraftWeightOptions.IsValid` rejects a negative weight, `Urgency` included.

# Invariants

- **No quantity is counted twice.** *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row
  D-01 asserts `Total` equals the sum of exactly the six addends, and D-02
  asserts changing only `ProjectedSeasonValue`'s display field cannot change
  `Total`.*
- **`RosterFit` is zero when an eligible starting slot is open.** *Check: row D-03.*
- **Replacement level is recomputed from the available pool after every pick**,
  never cached across picks. *Check: row D-04 asserts VAR for an unchanged player
  moves after an unrelated pick.*
- **Missing ADP yields `MarketValue = 0` plus an evidence item**, never an
  imputed ADP. *Check: row D-05.*
- **No ADP means no availability estimate**: `Urgency = 0` and
  `AvailableAtNextPick = null`, never an imputed survival probability.
  *Check: row D-11.*
- **A candidate near-certain to be gone before the next turn outranks an
  equal-value candidate likely to survive.** *Check: row D-10.*
- **Every `DraftValue` carries at least one evidence item.** *Check: row D-06.*
- **Weights come from options binding.** *Check: the canonical-value grep finds
  no weight literal in `src/`.*
- **Category leagues use per-category marginal value, not `Total`** — the MVP
  scores categories but does not rank them; a category league's draft board
  falls back to projected category totals with an explicit banner. *Check: row D-07.*

# Change procedure

The candidate's `HasUnverifiedContext` flag produces Context/Risk evidence
without changing any additive term. Consumers must not infer verification from
a nonzero projected score.

Changing a weight is a config change. Changing a *term* means updating this
file, the engine, the evidence strings it produces, and rows D-01 … D-12
together.

# Verification

`test_matrix_projection_draft.md`, rows D-01 through D-12, plus the R7 latency
assertion.

# Implementation evidence

`DraftValueCalculator` implements exactly the six additive terms with validated,
options-bound weights; decomposition-only fields never enter `Total`.
`DraftBoard` recomputes replacement level, positional scarcity, roster
redundancy, market value, and the urgency term from the currently available pool
on every call.
Tests cover D-01 through D-12 and rerank 300 players inside the 500 ms target.
Missing ADP is zero with explicit market evidence, and category leagues carry
the required fallback banner. This contract's done criteria are implemented.

## Owner-approved correction — 2026-09-23

The V1 formula read `(ADP − currentPick)`, the opposite of this section's own
definition ("positive when a player is available past their ADP"). On the real
2026 FantasyPros ADP it recommended Collin Gillespie and Saddiq Bey above Nikola
Jokić at pick 5, because a late ADP earned a bonus. The owner approved correcting
the formula to `(currentPick − ADP)`; code and tests changed in the same commit.
Evidence: `Market_value_rewards_players_past_their_adp_and_penalises_reaches`.
