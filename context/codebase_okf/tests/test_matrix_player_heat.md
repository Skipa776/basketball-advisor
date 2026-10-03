---
type: test_matrix
title: Test Matrix — Player Heat
description: Required boundaries and scoring checks for descriptive appearance-based heat.
tags: [tests, heat, matrix]
source_paths: [src/FantasyBasketball.Domain/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends/PlayerHeatTests.cs, tests/FantasyBasketball.Domain.Tests/Trends/HeatClassifierTests.cs]
depends_on: [required_gates.md, ../contracts/player_heat_contract.md]
status: implemented
last_updated: 2026-10-01
owners: [engineering]
---

# Required cases

| ID | Case | Expected | Required |
|---|---|---|---|
| `HE-01` | 9, 10, 12, 13, 30, 33, 34, 40 appearances | Exact current/recent/baseline boundaries; recent and baseline are disjoint | ✅ |
| `HE-02` | Recent production spike | Baseline unchanged, current average rises; dates and provenance retained | ✅ |
| `HE-03` | DNP, incomplete, future, other player, other season | Excluded; no previous-season fallback | ✅ |
| `HE-04` | Zero-valued appearances and zero baseline | Zero counts as played; relative lift absent at zero denominator | ✅ |
| `HE-05` | Negative scoring and negative baseline | No clamping; direction and relative lift have correct signs | ✅ |
| `HE-06` | Insufficient history | Null heat and qualification, never fabricated zero or cold label | ✅ |
| `HE-07` | Duplicates, unordered input, same-date games | Duplicates rejected; deterministic order and read-only evidence | ✅ |
| `HE-08` | Best versus hot, new games, changed scoring, ties | Separate fresh rankings; insufficient history excluded; stable ties | ✅ |
| `HE-09` | Invalid/custom policy, categories, invalid request identity/season | Validation or actual effective policy, no hardcoded call-site windows | ✅ |
| `HE-10` | Missing statistics, ambiguous DNP, missing provenance | Invalid observations rejected; incomplete observation remains explicit | ✅ |

# HOT and COLD labels

| ID | Case | Expected | Required |
|---|---|---|---|
| `HC-01` | Probabilities at 0.74, 0.75, 0.60, 0.59 | Enters at 0.75, holds at 0.60, leaves below it; a strong opposite reading switches | ✅ |
| `HC-02` | More minutes at the same per-minute rate | `HOT`, cause `Role` | ✅ |
| `HC-03` | Same minutes and usage, more points | `HOT`, cause `Shooting` | ✅ |
| `HC-04` | Half the minutes | `COLD`, cause `Role` | ✅ |
| `HC-05` | Under 15 games, or under 15 baseline minutes | No assessment | ✅ |
| `HC-06` | Steady production | No label, no cause | ✅ |

# Verification

`dotnet test --filter PlayerHeat`, plus the full repository gate. Tests are pure
and offline. These rows do not claim parser, persistence, API, live-data freshness,
or UI completion; their tests belong to the later integration slice.
