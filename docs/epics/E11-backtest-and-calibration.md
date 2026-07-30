# E11 — Back-test and calibration

**Requirements:** R18 · **Depends on:** E06

The epic that turns this project's two documented guesses into measured numbers. It
is also the strongest single thing on the roadmap for a portfolio: it converts
"transparent constants chosen by judgment" into "constants fitted on N seasons,
report committed."

## Pre-flight

E06 landed green · `scripts/gate.sh` green.

**Multiple seasons of per-game data must already be imported.** At 6 requests/minute
that is roughly 3.5 hours per season — three seasons is most of a day of wall-clock
scraping. Start the imports days before you start this epic; the harness against one
season measures nothing.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E11 in this repository: the back-test and calibration harness.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R18), and
   context/AGENT_CONTEXT_INDEX.md. Then read, in full:
     context/codebase_okf/contracts/backtest_contract.md
     context/codebase_okf/components/backtest_harness.md
     context/codebase_okf/tasks/run_backtest.md
     context/codebase_okf/contracts/projection_pipeline_contract.md
     context/codebase_okf/contracts/draft_value_contract.md
     context/codebase_okf/contracts/rolling_window_contract.md
     context/codebase_okf/tests/test_matrix_backtest.md

2. Confirm the imported season range FIRST and write down the eval season BEFORE
   running anything. Choosing the eval season after seeing results turns a
   back-test into a marketing exercise.

3. Scope, in order:
   a. BacktestRunner holding an AsOf date, wrapping every repository it touches so
      the wrapper THROWS on any row with a later SourceTimestamp, naming the row.
      Build this first. Leakage is the failure that makes a back-test worthless
      while looking excellent, so it must be an exception rather than a
      suspiciously strong metric nobody questions.
   b. AccuracyMetrics as a pure function of (projected[], actual[]): MAE, RMSE,
      Spearman rho (Pearson on ranks, ties averaged), top-K hit rate at K=100, and
      calibration by decile. Eligible players have >= 20 games in the eval season.
      Implement rank correlation directly — MathNet.Numerics is forbidden and the
      whole need is a few lines.
   c. Draft simulation reusing the PRODUCTION DraftValue and board code. A separate
      simulation implementation would measure the simulator rather than the
      product. Other teams pick by ADP + Normal(0, sigma); the user picks argmax
      DraftValue; the drafted roster is scored with the eval season's ACTUAL
      production. Fixed seed per draft index.
   d. WeightOptimizer: coordinate descent, 3 passes, each weight swept over
      {0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0}, 200 simulated drafts per evaluation.
      REPORT THE FULL SWEEP, not just the argmax — a flat coordinate means that
      weight does not matter, which is a finding worth publishing rather than a
      number worth tuning to noise.
   e. THE HONESTY RULE, enforced in code and not just documented: fitted weights
      may be written into the shipped defaults ONLY if they beat the judgment
      defaults on the HOLDOUT season. A fit that only wins on the season it was
      fitted against is overfitting with extra steps, and shipping it would be
      worse than shipping the honest guesses, because the numbers would then carry
      unearned authority.
   f. The report: docs/backtest/report-{season}.md with metrics, the decile table,
      the full weight sweep, the seed, the code commit, and the season range.
      Include the NAIVE BASELINE comparison — "assume last season repeats" as a
      projection — and report the delta. A model that cannot beat that has not
      earned its complexity, and saying so is more valuable than any number in the
      report.
   g. Update context/codebase_okf/assumptions.md: projection accuracy and draft
      weight calibration move from "knowingly unenforced" to measured, citing the
      report — or stay listed, citing the report that says the fit did not win.
      Either outcome is an update.

4. CLI-invoked, not an endpoint. It reads multiple seasons and takes minutes; nobody
   should be able to start one from a web request.

5. Required test rows: B-01 to B-10. Row B-04 feeds a perfect projection and asserts
   MAE 0, RMSE 0, rho 1, hit rate 1 — the identity case. Row B-07 asserts the
   honesty rule cannot be bypassed.

6. Commit after each of a-g. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

7. Budget ladder: (a) the AsOf wrapper; (b) accuracy metrics and the report;
   (c) simulation; (d) weight fitting. Accuracy metrics alone remove one of the two
   prose-only invariants and are worth landing on their own.

8. Non-negotiable: leakage throws; the objective scores actual production, never
   projected; the simulation uses production board code; the honesty rule is code;
   the committed report reproduces from the committed data and pinned commit; no
   placeholder files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, the actual metric values, whether the
fitted weights beat the defaults on holdout, whether the model beat the naive
baseline, and the one next step. Report the numbers as they came out — a
disappointing result honestly reported is the deliverable.
```

---

## Exit gate

Rows B-01…B-10 green, a committed report that reproduces, and
`assumptions.md` updated so that projection accuracy and weight calibration are
either measured or explicitly still-unvalidated with the report to prove it.
