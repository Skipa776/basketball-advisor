---
type: component
title: Draft Intelligence
description: Survival probability, tier detection, and the term replacement that retires the MVP's MarketValue proxy.
tags: [component, draft, domain]
source_paths: [src/FantasyBasketball.Domain/Draft, src/FantasyBasketball.Application/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests/Draft]
depends_on: [../contracts/draft_intelligence_contract.md, draft_engine.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - MarketValue no longer exists anywhere; OpportunityCost has taken its place.
  - Survival and tiering keep the board inside the R7 latency target.
---

# Responsibility

Owns *will he last until my next pick, and does waiting cost me anything*. Formulas
are owned by
[draft_intelligence_contract](../contracts/draft_intelligence_contract.md).

# The replacement, done properly

This component **retires** the MVP's `MarketValue` term rather than sitting beside
it. In the landing commit:

- `draft_value_contract.md` swaps the `MarketValue` row and `W_MARKET` for
  `OpportunityCost` and `W_OPPCOST`.
- `DraftValue`'s `MarketValue` property is renamed.
- The MVP's `valuePerPick` calculation is deleted.
- Rows D-01 and D-05 in
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) are
  updated to the new term.

Two live definitions of "the market says wait" is precisely the drift the
canonical-value rule exists to prevent, and a rename that leaves the old one
callable is not a replacement.

# Design

- `SurvivalModel` is pure: `(adp, sigma, pickNumber, recentPicks) → probability`.
  It reads no repository, which is what makes evaluating it across the whole pool
  per pick cheap enough.
- `TierDetector` derives its threshold from the current pool's gap distribution —
  no absolute constant, so it works in a 8-team and a 16-team league without
  tuning.
- Both recompute per pick with the board. Survival at pick 40 is a different number
  from survival at pick 25, and caching it is the same bug as caching replacement
  level.
- Category-aware value delegates entirely to
  [category_analyzer](category_analyzer.md); this component contributes the
  scarcity, fit, and opportunity-cost terms in z-units.

# Invariants

- **`MarketValue` exists nowhere after this lands.** *Check: a grep in the gate, plus
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md) row X-01.*
- **`SurvivalModel` is pure and repository-free.** *Check: its signature.*
- **Survival is monotone in pick number and bounded in `[0,1]`.** *Check: row X-02.*
- **Tier thresholds are pool-derived, not constant.** *Check: row X-06 asserts
  different league sizes produce different thresholds.*
- **Units never mix** — a category board carries no fantasy-point term. *Check: row X-07.*
- **Full-pool survival plus tiering stays within the R7 budget.** *Check: the
  latency assertion at realistic pool size.*

# Change procedure

Any change here that touches a `DraftValue` term updates
[draft_value_contract](../contracts/draft_value_contract.md) in the same commit.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md), rows
X-01 through X-07.
