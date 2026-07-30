---
type: test_matrix
title: Test Matrix — Projections, Context, Draft, Evidence
description: Required cases for the projection math, context application, draft value, and explainability.
tags: [tests, projections, draft, matrix]
source_paths: [src/FantasyBasketball.Domain/Projections, src/FantasyBasketball.Domain/Context, src/FantasyBasketball.Domain/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests]
depends_on: [required_gates.md, ../contracts/projection_pipeline_contract.md, ../contracts/draft_value_contract.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The cases gating build steps 10, 11, and 12 — requirements R7, R8, R9, R10.

# Projections (`P-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `P-01` | The contract's worked example, end to end | Reproduces to 4 dp: per-game `33.0825`, season `1984.9500`, `projGP 60` | ✅ |
| `P-02` | Persisted projection precision | `decimal(10,4)`, rounded half-away-from-zero at persistence only; intermediates unrounded | ✅ |
| `P-03` | Ratio stats in a projection | Recomputed from projected components, never projected directly | ✅ |
| `P-04` | Same inputs projected twice | Identical output; projector is pure | ✅ |
| `P-05` | Apply a context event, then remove it | Baseline row **byte-identical** before and after; adjusted rows are new | ✅ |
| `P-06` | `UsageDelta` and `ShotVolumeDelta` both set | Larger absolute delta wins; **not compounded** | ✅ |
| `P-07` | Five large stacked events | Multipliers stay in `[0.5, 1.5]`; minutes stay in `[0, 42]` | ✅ |
| `P-08` | Event past `ExpectedExpiration` | Excluded at computation time, with no cleanup job having run | ✅ |
| `P-09` | Any `AdjustedProjection` | Names its `BaselineProjectionId` and its applied event ids; never null | ✅ |
| `P-10` | Player with zero prior minutes | Projects to the shrinkage target; no `NaN`, no divide-by-zero | ✅ |

# Context (`C-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `C-01` | Any non-endpoint code path attempts `Verified` | Impossible: single assignment site, asserted by test and grep | ✅ |
| `C-02` | `Proposed` event | Applies, at reduced confidence, visibly marked unverified | ✅ |
| `C-03` | `Rejected` event | Never applies; row retained, not deleted | ✅ |
| `C-04` | Event with future `EffectiveFrom` | Does not apply yet | ✅ |
| `C-05` | User overrides default deltas | Defaults replaced entirely; override flag recorded | ✅ |
| `C-06` | Event created without a source | Rejected; manual events record `manual` | ✅ |
| `C-07` | Magnitude outside `[0,1]` | Clamped on write | ✅ |

# Draft (`D-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-01` | `DraftValue.Total` | Equals exactly the five addends; decomposition fields excluded | ✅ |
| `D-02` | Change only `ProjectedSeasonValue`'s display field | `Total` does not move — proves no double counting | ✅ |
| `D-03` | Player eligible for an open starting slot | `RosterFit == 0` | ✅ |
| `D-04` | Unrelated player is drafted | An untouched player's `VAR` changes — replacement level is recomputed | ✅ |
| `D-05` | Player with no ADP | `MarketValue == 0` plus a `Market` evidence item noting the absence | ✅ |
| `D-06` | Every draft recommendation | Carries ≥1 evidence item | ✅ |
| `D-07` | Category league draft board | Falls back to category totals with an explicit banner; does not rank by `Total` | ✅ |
| `D-08` | Pick then undo | Board equals its pre-pick snapshot exactly | ✅ |
| `D-09` | Drafted player | Leaves the available pool immediately; cannot be recommended | ✅ |
| — | Full-pool re-rank latency | Completes inside the R7 target on a realistic pool | ✅ |

# Evidence and confidence (`E-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `E-01` | Construct a `Recommendation` with empty evidence | Throws; the type cannot exist | ✅ |
| `E-02` | A displayed risk | Is a `Risk`-polarity evidence item, not UI prose | ✅ |
| `E-03` | Vary each confidence factor independently | Bucket moves as the factor model predicts | ✅ |
| `E-04` | Stale or failed contributing import | Confidence drops; a `DataQuality` evidence item appears | ✅ |
| `E-05` | Evidence ordering | Supporting by descending magnitude, then risks; deterministic | ✅ |

# Verification

`dotnet test --filter "Projections|Context|Draft|Recommendations"`, inside the
full gate.

# Current evidence

P-01 through P-10, C-01 through C-07, and D-01 through D-09 are implemented.
Projection coverage includes the worked example, four-record PostgreSQL
persistence, immutable baselines, non-compounding context, bounds, expiry, and
baseline/event links. Context coverage includes proposed/verified/rejected
review, sources, overrides, defaults, and magnitude clamping. Draft coverage
includes a complete 130-pick manual mock draft and the realistic-pool latency
assertion. E-01, E-02, E-03, and E-05 cover construction, risk polarity,
confidence buckets, and evidence ordering; E-04 awaits source-health
integration, so this matrix remains `partial`.
