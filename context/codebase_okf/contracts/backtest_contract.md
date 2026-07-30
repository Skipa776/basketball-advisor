---
type: contract
title: Back-testing and Calibration Contract
description: The holdout protocol, exact accuracy metric definitions, the weight-fitting objective and search, leakage prevention, and the honesty rule.
tags: [contract, backtest, calibration, math]
source_paths: [src/FantasyBasketball.Application/Backtest]
test_paths: [tests/FantasyBasketball.IntegrationTests/Backtest]
depends_on: [projection_pipeline_contract.md, draft_value_contract.md, rolling_window_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Data leakage from the evaluation season throws rather than degrading a metric.
  - The committed report reproduces from the committed data and pinned code.
  - Fitted weights ship only if they beat the judgment defaults on holdout.
---

# Responsibility

Owns the harness that turns this project's two documented guesses — *are the
projections any good* and *are the draft weights right* — into measured numbers.
Both are currently listed as knowingly unenforced in
[assumptions](../assumptions.md). This contract is what removes them from that
list.

# Protocol

```text
train seasons  = every imported season ≤ N − 1
eval season    = N                                    (holdout, never trained on)
```

Projections for the eval season are computed exactly as production computes them
([projection_pipeline_contract](projection_pipeline_contract.md)), using data
available **as of the eval season's opening day** and nothing after. Reported per
eval season; a multi-season run reports each separately and never pools them into
one flattering average.

Eligible players: `≥ MIN_EVAL_GAMES` (20) games in the eval season. Players who
never played cannot score a projection, and including them measures availability
rather than accuracy.

# Leakage prevention

Leakage is the failure mode that makes a back-test worthless while looking
excellent, so it is enforced structurally, not by discipline:

**The runner holds an `AsOf` date. Every repository call it makes goes through a
wrapper that throws when a returned row's `SourceTimestamp` is later than
`AsOf`.** A leak is an exception with the offending row named — not a suspiciously
good metric nobody questions.

# Accuracy metrics

On projected versus actual **fantasy points per game** under the seed league
([scoring_rules_catalog](scoring_rules_catalog.md)):

```text
MAE   = mean( |proj − actual| )
RMSE  = sqrt( mean( (proj − actual)² ) )

SpearmanRho = Pearson correlation of rank(proj) and rank(actual)
              (ties: average ranks)

TopKHitRate = |{ projected top K } ∩ { actual top K }| / K       K = 100

CalibrationByDecile = for each decile of projected value,
                      mean(actual) − mean(proj)
```

The decile calibration is the most useful of the four and the easiest to skip:
a model can post a fine MAE while systematically overrating the top decile, which
is exactly the decile a draft board is made of.

Rank correlation and the normal CDF are the only statistics needed, and both are
a handful of lines — see the `MathNet.Numerics` entry in
[`stack_config.toml`](../../../stack_config.toml).

# Weight fitting

The projection model is scored on accuracy; the **draft weights** cannot be,
because there is no ground-truth "correct weight." Score them on the outcome they
exist to produce:

```text
objective(weights) = mean over S simulated drafts of
                        realized season value of the user's starting lineup
                     S = 200
```

Simulation: other teams pick by `ADP + Normal(0, σ_ADP)`
([draft_intelligence_contract](draft_intelligence_contract.md)); the user picks
`argmax DraftValue` under the candidate weights; the drafted roster is then scored
with the eval season's **actual** production. Fixed seed per draft index, so a run
is reproducible.

Search: **coordinate descent**, 3 passes over the weights, each swept across
`{0, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0}`. That is ~84 objective evaluations rather
than the 2401 a full grid would need, and the objective is smooth enough in each
coordinate for it. Report the sweep, not just the argmax — a flat objective means
the weight does not matter and should be documented as such rather than tuned to
noise.

# The honesty rule

**If the fitted weights do not beat the judgment defaults on the holdout season,
the defaults ship.** The report records that outcome.

A fit that only wins on the season it was fitted against is overfitting with
extra steps, and shipping it would be worse than shipping the honest guesses —
because the numbers would then carry unearned authority. The same rule applies to
the trend weights in
[rolling_window_contract](rolling_window_contract.md).

# Output

- `docs/backtest/report-{evalSeason}.md` — committed: metrics, decile table,
  weight sweep, the seed, the code commit, and the imported season range.
- Fitted constants written into the shipped options defaults **in the same
  commit** as the report that justifies them.
- [assumptions](../assumptions.md) updated: the two prose-only invariants move to
  measured, citing the report.

# Invariants

- **Leakage throws.** *Check:
  [test_matrix_backtest](../tests/test_matrix_backtest.md) row B-01 seeds a row
  timestamped after `AsOf` and asserts the exception names it.*
- **Metrics match hand-computed values** on a fixed 20-player fixture. *Check: row B-02.*
- **Spearman handles ties** by average rank. *Check: row B-03.*
- **A perfect projection scores `MAE 0`, `RMSE 0`, `ρ 1`, `hit rate 1`.** *Check:
  row B-04 — the identity case.*
- **Simulation is reproducible** — same seed, same rosters, same objective.
  *Check: row B-05.*
- **The objective uses actual production, never projected.** *Check: row B-06
  perturbs projections only and asserts the objective is unchanged for a fixed
  drafted roster.*
- **The honesty rule is enforced in code**: a fit that does not beat defaults on
  holdout cannot be written into the defaults. *Check: row B-07.*
- **The committed report reproduces** from the committed data and pinned commit.
  *Check: row B-08 re-runs and diffs the metrics.*

# Change procedure

Changing a metric or the objective: this file, the harness, the fixture
expectations, and rows B-01 … B-08 — one commit. Re-running with new data
supersedes the report and updates the defaults and
[assumptions](../assumptions.md) together.

# Verification

[test_matrix_backtest](../tests/test_matrix_backtest.md), rows B-01 through B-08.
