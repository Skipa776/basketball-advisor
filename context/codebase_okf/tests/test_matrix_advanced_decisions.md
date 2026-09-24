---
type: test_matrix
title: Test Matrix — Categories, Draft Intelligence, Streaming, Trades
description: Required cases for the four advanced decision engines, including the identity and no-double-counting cases that catch most valuation bugs.
tags: [tests, categories, draft, streaming, trades, matrix]
source_paths: [src/FantasyBasketball.Domain/Categories, src/FantasyBasketball.Domain/Draft, src/FantasyBasketball.Domain/Streaming, src/FantasyBasketball.Domain/Trades]
test_paths: [tests/FantasyBasketball.Domain.Tests]
depends_on: [required_gates.md, ../contracts/category_value_contract.md, ../contracts/streaming_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Gates R12–R15. Four engines, one matrix, because they share the category math and
the starting-assignment routine — and a change to either must be checked against all
four.

# Categories (`Y-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `Y-01` | Player at 100% FT on two weekly attempts | `FT_PCT` z near zero, not maximal — volume weighting works | ✅ |
| `Y-02` | `TOV` z-score | Negative sign applied, in exactly one place | ✅ |
| `Y-03` | Pool percentage | Aggregate `Σ FGM / Σ FGA`, never the mean of player rates | ✅ |
| `Y-04` | `Φ` at `z ∈ {−3, −1, 0, 1, 1.96, 3}` | Matches published values to `1e-6` | ✅ |
| `Y-05` | Swap self and opponent | `P(win)` becomes `1 − p` | ✅ |
| `Y-06` | Punt detection with 5 weak categories | At most `PUNT_MAX` proposed; a user set overrides | ✅ |
| `Y-07` | Punted category | Contributes exactly zero to `CategoryValue` | ✅ |
| `Y-08` | Two identical teams | `P(win) == 0.5`, no divide-by-zero on zero variance | ✅ |
| `Y-10` | Roster change | Profile changes with no invalidation step | ✅ |
| `Y-11` | Detected punt, not yet accepted | No punt active; values unchanged | ✅ |
| `Y-12` | Points league | Analyzer never invoked | ✅ |

# Draft intelligence (`X-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `X-01` | `DraftValue.Total` after E06 | Equals the five addends including `OpportunityCost`; **no `MarketValue` term exists** | ✅ |
| `X-02` | `P(survive)` across pick numbers | Monotone decreasing, bounded `[0,1]` | ✅ |
| `X-03` | Pick number equals ADP | `P ≈ 0.5` | ✅ |
| `X-04` | Player with no ADP | `OpportunityCost == 0` plus a `Market` evidence item; never an imputed ADP | ✅ |
| `X-05` | Positional run in the last 6 picks | Opportunity cost rises for that position only | ✅ |
| `X-06` | Different league sizes | Tier thresholds differ — pool-derived, not constant | ✅ |
| `X-07` | Category-league board | Every `DraftValue` component is z-denominated; no points term | ✅ |
| — | Full-pool survival + tiering | Inside the R7 latency target at realistic pool size | ✅ |

# Streaming (`S-20`–`S-29`, `S-35`, `S-36`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `S-20` | 4-game player on full nights vs 3-game player on quiet nights | The 3-game player scores higher | ✅ |
| `S-21` | Daily slot assignment | Never over-fills a slot; never assigns an ineligible player | ✅ |
| `S-22` | `C`-only player vs higher-scoring `UTIL`-eligible player | Both started — most-constrained-first ordering holds | ✅ |
| `S-23` | Plan under a 4-acquisition limit | Never exceeds it; usage reported | ✅ |
| `S-24` | Move that would make the roster illegal | Never scored, never emitted | ✅ |
| `S-25` | Small streaming gain vs a valuable rostered player | No drop — protection holds | ✅ |
| `S-26` | Day with no open slots | Zero usable games, not a reduced positive | ✅ |
| `S-27` | No opponent data | `MatchupAdjustment == 1.0` plus evidence noting the absence | ✅ |
| `S-28` | Same inputs twice | Identical plan; ties broken by player id | ✅ |
| `S-29` | Any `UsableGame` | Carries a reason, usable or not | ✅ |
| `S-35` | Weekly-cadence league | Started player's usable games equal his scheduled games; benched equals zero | ✅ |
| `S-36` | No positive move available | Empty plan, not a forced move | ✅ |

# Trades (`R-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `R-01` | 2-for-1 | Replacement backfill credited for the vacated slot | ✅ |
| `R-02` | Receive a third startable center | Delta far below his raw value — displacement counted | ✅ |
| `R-03` | Trade leaving an illegal roster | `validation_failed`, never scored | ✅ |
| `R-04` | Category trade: raw value up, expected wins down | Verdict follows expected wins; divergence produces evidence | ⏳ category leagues deferred by the owner |
| `R-05` | Trade a player for himself | Exactly neutral — the identity case | ✅ |
| `R-06` | Three-team trade | Exactly one verdict, for the user | ✅ |
| `R-07` | Any evaluation | Carries evidence and a confidence level | ✅ |
| `R-08` | Same roster in the trade view and the lineup view | Both agree on the starting assignment | ✅ |
| `R-09` | Evaluate a trade | Nothing persisted; no roster row changed | ✅ |

# Verification

`dotnet test --filter "Categories|Draft|Streaming|Trades"`, inside the full gate.
