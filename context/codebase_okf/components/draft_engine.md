---
type: component
title: Draft Engine
description: Draft sessions, the live board, manual picks with undo, ranking, and explainable pick recommendations.
tags: [component, draft, recommendations]
source_paths: [src/FantasyBasketball.Domain/Draft, src/FantasyBasketball.Application/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests/Draft]
depends_on: [../contracts/draft_value_contract.md, ../contracts/recommendation_evidence_contract.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - A full draft runs end to end with manual pick entry only.
  - The board re-ranks the whole pool within the R7 latency target after each pick.
---

# Responsibility

Owns the live draft: session state, the available pool, re-ranking, and turning
`DraftValue` into ranked recommendations with evidence. The formula is owned by
[draft_value_contract](../contracts/draft_value_contract.md).

The product rule that shapes this component: **the highest-ranked player is not
automatically the recommended pick.** A board that just sorts by projection is
the thing this feature exists to beat.

# Design

- `DraftSession` owns pick order (snake or linear), the current pick, and the
  user's slot. `picksUntilNextTurn` is derived from that, and scarcity depends
  on it — see the contract.
- `DraftBoard` is a projection over session state, recomputed per pick, not a
  stored mutable table. Recomputation is the feature: replacement level,
  scarcity, and fit all move as the pool drains.
- `DraftRecommendationService` produces a ranked list, each item a
  `Recommendation` with evidence explaining *why this one now*.
- **Manual entry always works.** Provider-driven pick import is post-MVP; the
  manual path is not a fallback, it is the primary path.

# Recommendation shape

Recommendations distinguish, per the design doc's intent:

- best available by raw projection;
- best value at *this* pick, after scarcity, fit, and market;
- players available later, where `MarketValue` says waiting is cheap.

Survival probability and tier detection are **post-MVP** — the MVP expresses
"you can wait" through `MarketValue` and the ADP gap, not a probability model.

# Invariants

- **Re-ranking after every pick, from the available pool**, never an incremental
  patch of a cached list. *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row D-04.*
- **Full-pool re-rank inside the R7 latency target.** *Check: the latency
  assertion, run against a realistic pool size.*
- **Undo restores the exact prior board.** *Check: row D-08 snapshots the board,
  picks, undoes, and asserts equality.*
- **Duplicate pick submission is idempotent, conflicting submission is a
  conflict.** The live-draft double-click is a required test.
  *Check: [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md)
  row A-11.*
- **A drafted player leaves the available pool immediately** and cannot be
  recommended. *Check: row D-09.*
- **Every recommendation carries evidence.** *Check: row D-06.*
- **Category leagues fall back to category totals with an explicit banner**
  rather than silently ranking by an inapplicable `Total`. *Check: row D-07.*

# Change procedure

Changing ranking behavior: the contract first, then this engine, then the
affected rows in `test_matrix_projection_draft.md` — one commit.

# Verification

[test_matrix_projection_draft](../tests/test_matrix_projection_draft.md), rows
D-01 through D-09, plus rows A-11 and A-12.

# Implementation evidence

`DraftSession` owns sequential manual picks, snake-turn distance, duplicate
player rejection, and last-pick undo. `DraftBoard` always projects from session
state and never caches replacement level; drafted players leave immediately.
`DraftRecommendationEngine` converts every ranked value into a valid structured
recommendation. D-01 through D-09 pass, a 300-player rerank stays under 500 ms,
and a 10-team, 13-round manual mock draft completes all 130 picks with evidence
on every recommendation. API idempotency rows A-11/A-12 remain for the API
slice but do not block this component's two done criteria.
