---
type: task
title: Run the Back-test
description: The steps to produce an accuracy report and a weight fit, in an order that cannot leak the evaluation season.
tags: [task, backtest, calibration]
source_paths: [src/FantasyBasketball.Application/Backtest, docs/backtest]
test_paths: [tests/FantasyBasketball.IntegrationTests/Backtest]
depends_on: [../contracts/backtest_contract.md, ../components/backtest_harness.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The playbook for a back-test run. Metric definitions, the objective, and the honesty
rule are owned by [backtest_contract](../contracts/backtest_contract.md).

# Before you start

- **Per-game data for every season in scope must already be imported.** At 6
  requests per minute that is roughly 3.5 hours per season
  ([boxscore_importer](../components/boxscore_importer.md)). Three seasons is most
  of a day of wall-clock scraping. Start the import before you need the result.
- Decide the eval season and **write it down before running anything**. Choosing it
  after seeing results is how a back-test becomes a marketing exercise.

# Steps

1. **Confirm the data range.** `GET /api/imports/runs` — every season in scope has a
   completed box-score import. A partial season silently degrades every metric.
2. **Set `AsOf`** to the eval season's opening day. The runner clamps every
   repository read to it and throws on a later row, so this single value is what
   makes the run honest.
3. **Run accuracy.** Metrics over players with ≥20 games in the eval season. Read the
   **decile calibration first** — a good MAE with an overrated top decile is a bad
   draft board, and the top decile is what a draft board is made of.
4. **Run the weight fit.** Coordinate descent, 3 passes, 200 simulated drafts per
   evaluation. Record the whole sweep.
5. **Apply the honesty rule.** Fitted weights ship only if they beat the judgment
   defaults *on the holdout season*. If they do not, the defaults stand and the
   report says so. The harness enforces this; do not work around it.
6. **Commit the report** to `docs/backtest/report-{season}.md` with the seed, the code
   commit, and the season range. If the weights won, update the shipped option
   defaults **in the same commit**.
7. **Update [assumptions](../assumptions.md).** Projection accuracy and draft weight
   calibration move from "knowingly unenforced" to measured, citing the report — or
   stay listed, citing the report that says the fit did not win. Either outcome is
   an update.

# Reading the results honestly

- A **flat coordinate** in the sweep means that weight does not matter. Report it as
  such; do not tune it to noise.
- A **large MAE with a strong `ρ`** means the model ranks well and scales badly —
  a draft board cares about rank, so this is a better result than it looks.
- A **strong MAE with a weak `ρ`** is the reverse and is worse than it looks.
- **Improvement over a naive baseline is the real bar.** Compute last season's
  per-game value as a baseline projection and report the delta. A model that cannot
  beat "assume last year repeats" has not earned its complexity, and saying so
  in the report is more valuable than any number in it.

# Cadence

Once per completed NBA season, and after any change to the projection or draft math.
A change to a formula invalidates the fitted weights, and the change procedure in
the affected contract says so.

# Verification

[test_matrix_backtest](../tests/test_matrix_backtest.md). Row B-08 re-runs the
committed report and diffs the metrics — the report is only trustworthy because it
reproduces.
