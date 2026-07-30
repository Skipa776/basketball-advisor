---
type: component
title: Projection Engine
description: Computes baseline projections from observed stats using per-minute rates and projected minutes.
tags: [component, projections, domain]
source_paths: [src/FantasyBasketball.Domain/Projections, src/FantasyBasketball.Application/Projections]
test_paths: [tests/FantasyBasketball.Domain.Tests/Projections]
depends_on: [../contracts/projection_pipeline_contract.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - The contract's worked example reproduces to 4 decimal places.
  - Baseline computation is pure; no context, no news, no league.
---

# Responsibility

Owns observed stats → `BaselineProjection`. All math, constants, and the worked
example are owned by
[projection_pipeline_contract](../contracts/projection_pipeline_contract.md);
this file owns how the code is arranged and what must not leak in.

# Design

- `MinutesProjector` — step 3 only, isolated because it is the variable context
  events most often modify and the one most likely to become an ML model later.
- `BaselineProjector` — steps 1, 2, 4, 5, 6.
- League-average rates are computed once per pool and passed in, never queried
  per player. That is both a correctness property (every player shrinks toward
  the same league) and the reason a full-pool projection stays fast.
- `ModelVersion` is stamped on every `BaselineProjection` so a projection can be
  traced to the algorithm that produced it. Bumped whenever a constant or a step
  changes.

# Invariants

- **The baseline is statistics only.** No context events, no news, no league
  scoring, no roster. A baseline that knew about a trade would make the
  decomposition in R8 meaningless. *Check: the projector's signature takes no
  context or league argument — a compile-time guarantee, not a convention.*
- **Pure and deterministic**, given the pool average. *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row P-04.*
- **Baselines are append-only.** *Check: rows P-05, A-04.*
- **Zero minutes yields a zero projection, not a divide-by-zero.** A player with
  no history projects to the shrinkage target, not `NaN`. *Check: row P-10.*
- **Every constant comes from bound options**, never a literal. *Check: the
  canonical-value grep.*
- **`ModelVersion` changes whenever the math changes.** *Check: review, tied to
  the change procedure below.*

# Change procedure

Changing the math: the contract (including recomputing its worked example by
hand), this component, `ModelVersion`, and every projection golden — one commit.

# Verification

[test_matrix_projection_draft](../tests/test_matrix_projection_draft.md), rows
P-01 through P-10.

# Implementation evidence

`LeagueAverageRateCalculator`, `MinutesProjector`, and `BaselineProjector` are
pure Domain code. The worked example reproduces per-game `33.0825`, season
`1984.9500`, and 60 projected games exactly; tests also cover derived ratios,
repeatability with a caller-supplied id, and zero prior minutes. All constants
come from validated, options-bound `ProjectionOptions`. `ProjectionService`
computes the pool average once, injects one UTC run timestamp, and persists one
observed/baseline pair per player. This component's baseline-only done criteria
are implemented.
