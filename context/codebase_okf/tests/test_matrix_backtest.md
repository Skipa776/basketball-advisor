---
type: test_matrix
title: Test Matrix — Back-testing
description: Required cases for leakage prevention, metric correctness, simulation reproducibility, and the honesty rule.
tags: [tests, backtest, calibration, matrix]
source_paths: [src/FantasyBasketball.Application/Backtest]
test_paths: [tests/FantasyBasketball.IntegrationTests/Backtest]
depends_on: [required_gates.md, ../contracts/backtest_contract.md]
status: partial
last_updated: 2026-10-01
owners: [engineering]
---

# Responsibility

Gates R18. Row `B-01` is the one that decides whether any number this harness
produces means anything.

# Leakage (`B-01`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `B-01` | A row timestamped after `AsOf` reachable by the runner | Throws, naming the offending row. **Not** a silently better metric | ✅ |

Leakage is the failure that makes a back-test worthless while looking excellent, so
it is a thrown exception rather than a review item.

# Metrics (`B-02`–`B-04`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `B-02` | Fixed 20-player fixture | MAE, RMSE, `ρ`, top-K hit rate, and decile calibration match hand-computed values | ✅ |
| `B-03` | Tied projected values | Spearman uses average ranks | ✅ |
| `B-04` | Projection equal to actual for every player | `MAE 0`, `RMSE 0`, `ρ 1`, hit rate `1`, decile deviations all `0` — the identity case | ✅ |

# Runner (`BR-01`–`BR-04`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `BR-01` | Season lines for the year before eval, eval box scores with one player under 20 games | Model and "last season repeats" scored against actual points per game; the under-20 player is excluded | ✅ |
| `BR-02` | An eval-season player with no training line | Skipped, not scored as zero | ✅ |
| `BR-03` | No training lines for the year before eval | Throws, naming the missing season | ✅ |
| `BR-04` | Render a result twice | Identical markdown: metric table with model − baseline deltas, decile calibration, as-of, commit; LF line endings | ✅ |

# Simulation and fitting (`B-05`–`B-07`, `B-09`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `B-05` | Same seed, two runs | Identical drafted rosters and identical objective values | ✅ |
| `B-06` | Perturb projections, hold the drafted roster fixed | Objective unchanged — it scores **actual** production, never projected | ✅ |
| `B-07` | A fit that loses to the judgment defaults on holdout | Cannot be written into the shipped defaults; the honesty rule is code, not prose | ✅ |
| `B-09` | Reference graph | No duplicate `DraftValue` implementation — the simulation uses production code | ✅ |

# Draft benchmark (`DB-01`–`DB-02`)

The board-versus-ADP benchmark reuses `SimulatedOpponent` and `DraftBoard` (B-09) and
reports the mean per-draft lineup difference with a 95% interval.

| ID | Case | Expected | Required |
|---|---|---|---|
| `DB-01` | Projections equal actual; some ADPs 40 picks late | Board beats the ADP-only bot; the 95% interval's low end is above 0 | ✅ |
| `DB-02` | Five pure centers in the seed league | Lineup value counts C and both UTIL; bench players score nothing | ✅ |

# Reproducibility and reach (`B-08`, `B-10`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `B-08` | Re-run the committed report at its recorded commit and seed | Metrics match the committed report | ✅ |
| `B-10` | Every HTTP endpoint | None invokes the runner — it is CLI-only | ✅ |

# The baseline comparison

Not a pass/fail row, but required in every report
([run_backtest](../tasks/run_backtest.md)): compute *"assume last season repeats"*
as a baseline projection and report the delta against it. A model that cannot beat
that baseline has not earned its complexity, and the report is where that gets said
out loud.

# Verification

`dotnet test --filter Backtest`, inside the full gate. Uses committed fixture
seasons; never scrapes.
