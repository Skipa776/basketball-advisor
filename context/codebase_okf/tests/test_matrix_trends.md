---
type: test_matrix
title: Test Matrix — Trends
description: Required cases for rolling windows, the decomposition identity, sustainability classification, and free-agent ranking.
tags: [tests, trends, matrix]
source_paths: [src/FantasyBasketball.Domain/Trends, src/FantasyBasketball.Application/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends]
depends_on: [required_gates.md, ../contracts/rolling_window_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Gates R11. The decomposition identity (T-02) is the row that makes every other
trend number trustworthy.

# Decomposition and score (`T-01`–`T-06`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `T-01` | The contract's worked example | Reproduces to 4 dp: `FromMinutes 8.6000`, `FromUsage 0.4500`, `FromEfficiency 3.7500`, `TrendScore 10.3625` | ✅ |
| `T-02` | 200 generated baseline/window pairs | `FromMinutes + FromUsage + FromEfficiency == ΔV` within `1e-9` for every pair | ✅ |
| `T-03` | Two players, identical `ΔV`, opposite decompositions | Opposite sustainability labels — proves the classifier never reads total change | ✅ |
| `T-04` | Zero minutes, or zero usage, in a window | `Unproven`, no score, no divide-by-zero | ✅ |
| `T-05` | Constant production across both periods | `TrendScore == 0` — only holds if baseline excludes the window | ✅ |
| `T-06` | Any trend | Carries evidence naming the dominant term | ✅ |

# Engine behaviour (`T-10`–`T-14`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `T-10` | A new game is imported | Ranking changes with no cache-invalidation step — rankings are queries | ✅ |
| `T-11` | Player with no per-game data | Absent from trends entirely, not zero-scored | ✅ |
| `T-12` | `Unproven` player with a large production jump | Does not appear in the risers list | ✅ |
| `T-13` | Free-agent bucketing | Deterministic; boundaries read from bound options | ✅ |
| `T-14` | Rebuild windows twice | Byte-identical rows — rebuild is idempotent | ✅ |

# Fixtures

Per-game rows come from the committed box-score fixtures
(`test_matrix_ingestion_scrapers.md`). Trend tests never scrape and never touch a
database — the decomposition is pure and its tests should prove that by not needing
either.

# Verification

`dotnet test --filter Trends`, inside the full gate.
