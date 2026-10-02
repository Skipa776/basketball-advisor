---
type: contract
title: Rolling Window and Trend Contract
description: Window definitions, the exact three-way production decomposition, sustainability classification, and the trend score formula.
tags: [contract, trends, math]
source_paths: [src/FantasyBasketball.Domain/Trends, src/FantasyBasketball.Application/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends]
depends_on: [stat_vocabulary.md, projection_pipeline_contract.md]
status: partial
last_updated: 2026-10-01
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - The decomposition identity holds exactly for every player and window.
  - Sustainability is derived from the decomposition, never from the size of the production change.
  - The worked example below reproduces to 4 decimal places.
---

# Responsibility

Owns rolling windows and the riser/faller math. The product rule this contract
exists to enforce: **a player is not a riser because production went up — he is a
riser because opportunity went up.** A 35% production jump on unchanged minutes
and unchanged usage is a shooting streak, and the model must say so.

# Windows

```text
WindowSpan = Season | Last30Days | Last14Days | Last7Days | Last10Games | Last5Games
```

Each window is materialized per player per span, keyed on the through-date.

**The baseline is the season through the day before the window opens** — never
season-to-date including the window, which would compare a period against
itself. A baseline with fewer than `MIN_BASELINE_GAMES` (10) games yields
`Sustainability.Unproven` and no trend score.

# The decomposition

Fantasy value per game factors exactly:

```text
V = M × Usg × Eff

  M    minutes per game
  Usg  usage per minute = (FGA + 0.44 × FTA + TOV) / MIN
  Eff  fantasy points per unit of usage = (V / M) / Usg
```

The change from baseline (`₀`) to window (`₁`) splits into three additive terms
using the symmetric (midpoint) attribution:

```text
R = V / M                                      fantasy points per minute

FromMinutes    = (M₁   − M₀)   × (R₀   + R₁)/2
FromUsage      = (Usg₁ − Usg₀) × (Eff₀ + Eff₁)/2 × (M₀ + M₁)/2
FromEfficiency = (Eff₁ − Eff₀) × (Usg₀ + Usg₁)/2 × (M₀ + M₁)/2
```

**These sum to `V₁ − V₀` exactly** — not approximately. The identity is
algebraic, not empirical, and is a required test.

```text
OpportunityShare = (FromMinutes + FromUsage)
                 / (|FromMinutes| + |FromUsage| + |FromEfficiency|)
```

Absolute values in the denominator keep the share bounded in `[-1, 1]` and stop
it exploding when total change is near zero.

# Sample weight

```text
SampleWeight = min(1, minutesInWindow / K_WINDOW)      K_WINDOW = 150
```

# Sustainability

Derived from the decomposition and the sample, in this order:

| Condition | Result |
|---|---|
| `SampleWeight < 0.35` or baseline `< MIN_BASELINE_GAMES` | `Unproven` |
| `OpportunityShare ≥ 0.60` | `OpportunityDriven` |
| `OpportunityShare ≤ 0.30` | `EfficiencyDriven` |
| otherwise | `Mixed` |

`OpportunityDriven` is the signal worth acting on. `EfficiencyDriven` is the
regression-risk flag, and it is what the design doc's Player A example
describes.

# Trend score

```text
TrendScore = ( W_OPP × (FromMinutes + FromUsage)
             + W_EFF × FromEfficiency
             − W_ROLE × RoleRisk × |V₁ − V₀| ) × SampleWeight
```

| Constant | Value | Meaning |
|---|---|---|
| `W_OPP` | `1.0` | Opportunity change counts at face value |
| `W_EFF` | `0.35` | Efficiency change counts, discounted for regression |
| `W_ROLE` | `0.5` | Role risk discounts a move in either direction |
| `K_WINDOW` | `150` | Minutes for full sample weight |
| `MIN_BASELINE_GAMES` | `10` | Baseline games required to trend at all |

Units are **fantasy points per game**, so the score is directly interpretable
and comparable to `DraftValue` terms. `RoleRisk` comes from the adjusted
projection ([projection_pipeline_contract](projection_pipeline_contract.md)).

Weights are judgment values pending
[backtest_contract](backtest_contract.md) calibration — see
[assumptions](../assumptions.md).

# Worked example

Required test (row T-01). Verified arithmetic:

```text
Baseline (season, ≥10 games)   M₀ 24.0   V₀ 24.0   FGA 12.0  FTA 2.5  TOV 1.5
Window   (last 6 games)        M₁ 32.0   V₁ 36.8   FGA 16.0  FTA 4.0  TOV 2.0

R₀   1.0000   R₁   1.1500
Usg₀ 0.6083   Usg₁ 0.6175
Eff₀ 1.6438   Eff₁ 1.8623

ΔV              12.8000
FromMinutes      8.6000
FromUsage        0.4500
FromEfficiency   3.7500
                --------
identity residual  0        (exact)

OpportunityShare 0.7070  → OpportunityDriven
SampleWeight     1.0000  (192 window minutes ≥ 150)
TrendScore      10.3625  (RoleRisk 0)
```

Read it as the model is meant to: production rose 12.8 points per game, and
**8.6 of that is simply more minutes** — the sustainable part — while 3.75 is
efficiency that may not hold.

# Invariants

- **The identity holds exactly.** *Check:
  [test_matrix_trends](../tests/test_matrix_trends.md) row T-02 asserts
  `FromMinutes + FromUsage + FromEfficiency == ΔV` within `1e-9` on 200
  generated player pairs.*
- **Sustainability never reads total production change.** The classifier takes
  the decomposition and sample only. *Check: row T-03 constructs two players
  with identical ΔV and opposite decompositions and asserts opposite labels.*
- **Zero minutes or zero usage never divides by zero** — such a window is
  `Unproven` with no score. *Check: row T-04.*
- **The baseline excludes the window.** *Check: row T-05 asserts a player with
  constant production has `TrendScore == 0`, which only holds if the periods are
  disjoint.*
- **Every trend carries evidence** naming the dominant term. *Check: row T-06.*
- **Constants come from bound options.** *Check: the canonical-value grep.*

# Change procedure

Changing a constant or a term: this file (recomputing the worked example by
hand), the engine, `ModelVersion` on trends, and rows T-01 … T-06 — one commit.

# Verification

[test_matrix_trends](../tests/test_matrix_trends.md), rows T-01 through T-06.

## Separate owner-selected descriptive heat

[Player heat](player_heat_contract.md) owns the latest-three-appearance comparison
against a disjoint expanding/capped baseline. It is not `TrendScore` and does not
change the opportunity or sustainability formulas above. Its effective policy
expresses the three-game window directly; it never aliases `Last5Games`.
