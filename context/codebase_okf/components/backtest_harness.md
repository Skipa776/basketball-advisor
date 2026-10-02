---
type: component
title: Back-test Harness
description: The as-of clamped runner, accuracy metrics, draft simulation and weight search, and the committed report.
tags: [component, backtest, calibration]
source_paths: [src/FantasyBasketball.Application/Backtest]
test_paths: [tests/FantasyBasketball.IntegrationTests/Backtest]
depends_on: [../contracts/backtest_contract.md, boxscore_importer.md]
status: partial
last_updated: 2026-10-01
owners: [engineering]
risk_level: high
done_criteria:
  - Leakage is impossible to introduce without an exception.
  - A run produces a committed report and, if justified, updated shipped defaults.
---

# Responsibility

Owns the harness that measures the projection model and fits the draft weights.
Protocol, metrics, objective, and the honesty rule are owned by
[backtest_contract](../contracts/backtest_contract.md).

# Design

- `BacktestRunner` holds an `AsOf` date and wraps every repository it touches. **The
  wrapper throws on any row with a later `SourceTimestamp`** — leakage is an
  exception naming the row, not a suspiciously strong metric.
- `AccuracyMetrics` is a pure function of `(projected[], actual[])`. No repository,
  no clock, so it is testable against hand-computed fixtures.
- `WeightOptimizer` runs coordinate descent over the simulated-draft objective, with
  a fixed seed per draft index. It reports the **full sweep**, not just the argmax —
  a flat coordinate means that weight does not matter, which is a finding worth
  publishing rather than a number worth tuning.
- Draft simulation reuses the **production** `DraftValue` and board code. A separate
  simulation implementation would measure the simulator, not the product.
- It runs as a CLI-invoked job, not an endpoint. It reads multiple seasons and takes
  minutes; nobody should be able to start one from a web request.

# Requires the box-score importer first

Multi-season per-game data is the input, and that is hours of rate-limited scraping
([boxscore_importer](boxscore_importer.md)). The importer is a hard prerequisite —
the harness against one season of season-aggregates measures nothing.

# Output

`docs/backtest/report-{season}.md`, committed: metrics table, decile calibration,
the weight sweep, the seed, the code commit, and the season range. If the fitted
weights beat the judgment defaults on holdout, the same commit updates the shipped
option defaults and moves the two prose-only invariants in
[assumptions](../assumptions.md) to measured, citing the report. If they do not, the
report says so and the defaults stand.

# Invariants

- **Leakage throws with the row named.** *Check:
  [test_matrix_backtest](../tests/test_matrix_backtest.md) row B-01.*
- **Metrics are pure and match hand-computed fixtures.** *Check: rows B-02 … B-04.*
- **Simulation reuses production board code.** *Check: row B-09 asserts no duplicate
  `DraftValue` implementation exists.*
- **Runs are reproducible from the seed.** *Check: row B-05.*
- **The honesty rule is enforced in code.** *Check: row B-07.*
- **It is not reachable over HTTP.** *Check: row B-10 asserts no endpoint invokes the
  runner.*
- **The committed report reproduces.** *Check: row B-08.*

# Change procedure

Changing a metric or the objective: the contract first, then this harness, then the
fixtures and rows. A new run supersedes the report and updates defaults and
assumptions together.

# Verification

[test_matrix_backtest](../tests/test_matrix_backtest.md), rows B-01 through B-10.
