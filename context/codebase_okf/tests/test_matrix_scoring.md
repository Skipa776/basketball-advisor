---
type: test_matrix
title: Test Matrix — Scoring
description: Required cases for the scoring engine, league validation, and ratio aggregation.
tags: [tests, scoring, matrix]
source_paths: [src/FantasyBasketball.Domain/Scoring]
test_paths: [tests/FantasyBasketball.Domain.Tests/Scoring]
depends_on: [required_gates.md, ../contracts/scoring_rules_catalog.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The cases that must pass before the scoring engine commits — build step 3, and
the gate on requirement R5.

# Matrix

| ID | Case | Expected | Required |
|---|---|---|---|
| `S-01` | League with zero scoring rules | Validation error; **no provider default substituted** | ✅ |
| `S-02` | Points league with no rules / category league with no categories | Validation error naming the missing field | ✅ |
| `S-03` | Two scoring rules for the same stat | Validation error; not silent last-wins | ✅ |
| `S-04` | Same stat line scored twice | Identical result; engine is pure | ✅ |
| `S-05` | Stat line containing `MIN`, `FGM`, `PF` under the seed league | Those stats contribute exactly zero | ✅ |
| `S-06` | Line with high `TOV` and low production | Total is **negative**, not clamped to zero | ✅ |
| `S-07` | Aggregate two stat lines, read `FG_PCT` | Equals combined `FGM/FGA`, **not** the mean of the two ratios | ✅ |
| `S-08` | Player with `FGA = 0` | `FG_PCT` is `0m`; no divide-by-zero, no null | ✅ |

# Seed-league golden

The canonical worked case, using the seed league from
[scoring_rules_catalog](../contracts/scoring_rules_catalog.md):

```text
input   PTS 17.25  REB 5.85  AST 4.275  STL 1.125  BLK 0.6  TOV 2.775
        (plus MIN 30.0 and FGM 6.5, which have no rule)

        PTS 17.2500 × 1.0  = +17.2500
        REB  5.8500 × 1.2  =  +7.0200
        AST  4.2750 × 1.5  =  +6.4125
        STL  1.1250 × 3.0  =  +3.3750
        BLK  0.6000 × 3.0  =  +1.8000
        TOV  2.7750 × −1.0 =  −2.7750
        MIN, FGM           =   0

expect  33.0825
```

This is the same line the projection contract's worked example produces, so the
two goldens verify each other end to end.

# Category cases

| ID | Case | Expected | Required |
|---|---|---|---|
| `S-07` | (above) covers ratio aggregation | | ✅ |
| — | Category league returns per-category totals for the configured nine | Totals only; **no ranking or comparison** | ✅ |

# Verification

`dotnet test --filter Scoring`, run inside the full gate.
