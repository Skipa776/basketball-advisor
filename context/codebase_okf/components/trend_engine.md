---
type: component
title: Trend Engine
description: Builds rolling windows, decomposes production change, and ranks risers, fallers, and free agents.
tags: [component, trends, domain]
source_paths: [src/FantasyBasketball.Domain/Trends, src/FantasyBasketball.Application/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends]
depends_on: [../contracts/rolling_window_contract.md, boxscore_importer.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - Every trend states which term drove it, in the evidence.
  - Rankings are recomputed from windows, never cached as a leaderboard.
---

# Responsibility

Owns risers, fallers, and free-agent ranking. Math is owned by
[rolling_window_contract](../contracts/rolling_window_contract.md); per-game input
comes from [boxscore_importer](boxscore_importer.md).

# Design

- `WindowBuilder` materializes each `(player, span, throughDate)` window from
  per-game rows. Windows are **derived data**: recomputed after an import, never
  hand-edited, and safe to delete and rebuild.
- `TrendAnalysisService` computes the decomposition and score. Pure given the
  window pair — no repository access inside the math.
- Rankings (risers, fallers, free agents) are **queries over windows**, not stored
  lists. A stored leaderboard is a cache that goes stale the moment a game ends and
  is the fastest way to show a user a "riser" who is now injured.
- Free-agent ranking combines trend score with rest-of-season value and roster fit,
  producing the design doc's buckets: `StrongAdd`, `SpeculativeAdd`, `Streamer`,
  `WatchList`, `Hold`, `DropCandidate`.
- `TrendRefreshWorker` rebuilds windows after each box-score import batch, not on a
  timer — recomputing windows when no new games exist is pure waste.

# Evidence

Every trend produces evidence naming **which term dominated**, because that is the
entire insight:

```text
+ minutes up 24.0 → 32.0 (8.6 pts/game of the 12.8 gain)
+ usage up 0.608 → 0.618
- 3.75 pts/game of the gain is efficiency, which may regress
- 6-game sample
```

A `TrendBadge` in the UI shows the `Sustainability` label; the numbers behind it
are always one click away, never hidden.

# Invariants

- **Rankings are computed, never stored.** *Check:
  [test_matrix_trends](../tests/test_matrix_trends.md) row T-10 asserts a new game
  changes the ranking with no cache invalidation step.*
- **Every trend carries evidence naming the dominant term.** *Check: row T-06.*
- **A player with no per-game data is absent from trends**, not zero-scored.
  *Check: row T-11.*
- **`Unproven` players never appear in the risers list.** *Check: row T-12.*
- **Free-agent buckets are deterministic and their boundaries are options-bound.**
  *Check: row T-13.*
- **The decomposition math lives in Domain and takes no repository.** *Check: its
  signature.*
- **Window rebuild is idempotent** — rebuilding twice yields identical rows.
  *Check: row T-14.*

# Change procedure

Changing bucket thresholds is a config change. Changing the score means the
contract first, then this engine, then the rows.

# Verification

[test_matrix_trends](../tests/test_matrix_trends.md), rows T-06, T-10 through T-14.
