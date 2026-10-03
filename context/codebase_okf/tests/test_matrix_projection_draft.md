---
type: test_matrix
title: Test Matrix — Projections, Context, Draft, Evidence
description: Required cases for the projection math, context application, draft value, and explainability.
tags: [tests, projections, draft, matrix]
source_paths: [src/FantasyBasketball.Domain/Projections, src/FantasyBasketball.Domain/Context, src/FantasyBasketball.Domain/Draft]
test_paths: [tests/FantasyBasketball.Domain.Tests]
depends_on: [required_gates.md, ../contracts/projection_pipeline_contract.md, ../contracts/draft_value_contract.md]
status: implemented
last_updated: 2026-09-26
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
| `P-11` | Multiple league publications, manual overrides, changed scoring and a different season pool | Current values follow the selected run, exact observation and profile; old baselines are unchanged; random IDs do not establish recency | ✅ |
| `P-12` | Publication fails after baseline writes | Transaction rolls back observed, baseline, adjusted and value rows together | ✅ |
| `P-13` | Real HTTP projection publication, pick and undo | League receives ranked values/evidence; pick removes player; undo restores board; scoring edit hides obsolete values | ✅ |
| `P-14` | Publication with the rate, minutes, availability and covariance models active | The baseline carries hierarchical minutes, rates and per-game values, games played = the Beta-Binomial mean rounded, a `+`-joined model version, and one distribution stored beside it | ✅ |
| `P-15` | Real database publication with the fitted golden parameters | `projection_distribution` holds one row per baseline; the league's fantasy value carries a per-game SD; the draft candidate exposes a season-value distribution consistent with the published value | ✅ |

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
| `D-01` | `DraftValue.Total` | Equals exactly the four addends (VAR, scarcity, fit, risk); decomposition fields excluded | ✅ |
| `D-02` | Change only `ProjectedSeasonValue`'s display field | `Total` does not move — proves no double counting | ✅ |
| `D-03` | Player eligible for an open starting slot | `RosterFit == 0` | ✅ |
| `D-04` | Unrelated player is drafted | An untouched player's `VAR` changes — replacement level is recomputed | ✅ |
| `D-06` | Every draft recommendation and heuristic board value | Carries ≥1 evidence item; no heuristic value carries a `Market` (ADP or survival) item | ✅ |
| `D-15` | Simulated board without distributions, without a schedule, and with both | Null board with the reason; with both, 15 candidates ranked by the requested risk mode and the user's next pick | ✅ |
| `D-16` | Draft recommendations with a simulation | Lead with the simulated top pick ("Best simulated pick" evidence), others carry survival odds; the rest of the pool follows | ✅ |
| `D-07` | Category league draft board | Falls back to category totals with an explicit banner; does not rank by `Total` | ✅ |
| `D-14` | Bench-phase simulated team already holding 3 pure centers | Takes a non-center instead of a 4th center when one is available | ✅ |
| `D-08` | Pick then undo | Board equals its pre-pick snapshot exactly | ✅ |
| `D-09` | Drafted player | Leaves the available pool immediately; cannot be recommended | ✅ |
| — | Full-pool re-rank latency | Completes inside the R7 target on a realistic pool | ✅ |

# Lineups and simulation (`LO-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `LO-01` | 500 random rosters of 8–13 players with 1–2 positions on PG/SG/SF/PF/C/G/F/3×UTIL | The lineup equals exact matching (bitmask dynamic program) every time | ✅ |
| `LO-02` | Daily lineup, one UTIL slot, two same-team players and one on another team | Each scheduled day starts the best player with a game | ✅ |
| `LO-03` | Weekly lineup | Values weighted by that week's games; a team with no games that week scores nothing | ✅ |
| `DS-01` | The same draft state and seed, simulated twice | Identical boards | ✅ |
| `DS-02` | Runner-up's edge | Its standard error is below the independent-draws SE: common random numbers pair the rollouts | ✅ |
| `DS-03` | C + PG slots: C1 (100) vs PG1 (105) with PG2 and PG3 (104, 103) behind and C2 (20) as the only other center | The simulator takes C1; PG1's edge is below −50 | ✅ |
| `DS-04` | Two players with equal means, SD ≈ 0 vs SD 40 | Cautious ranks the steady one first, Upside the volatile one | ✅ |
| `DS-05` | Survival to the user's next pick | Read from the top pick's rollouts: under 5% for a player the opponents take first, over 95% for one they pass | ✅ |
| `DS-06` | First pick of a 12-team, 13-round draft: 300 players, 30 teams × 165 days, K 15, N 500 | p95 of ten boards under 2 s (after one warm-up), in the gate | ✅ |
| `LO-04` | A 10:30 pm Eastern tip-off; a player with no team | Counted on its Eastern date; the no-team player gets the median team's schedule | ✅ |

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

P-01 through P-10, C-01 through C-07, and D-01 through D-14 are implemented.
Projection coverage includes the worked example, four-record PostgreSQL
persistence, immutable baselines, non-compounding context, bounds, expiry, and
baseline/event links. Context coverage includes proposed/verified/rejected
review, sources, overrides, defaults, and magnitude clamping. Draft coverage
includes a complete 130-pick manual mock draft and the realistic-pool latency
assertion. E-01, E-02, E-03, and E-05 cover construction, risk polarity,
confidence buckets, and evidence ordering. E-04 is implemented by the
Application draft surface: stale or failed automated imports lower the
freshness factor and append structured `DataQuality` risk evidence.

The completed-draft regression verifies that a pick beyond the configured rounds
is rejected without changing the pick list and that undo permits a replacement.
