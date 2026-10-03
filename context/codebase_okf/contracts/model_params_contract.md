---
type: contract
title: Model Parameters Contract
description: How offline-fitted model parameters are versioned, activated and verified between Python (tools/modeling) and C#.
tags: [contract, modeling, persistence, statistics]
source_paths: [src/FantasyBasketball.Domain/Modeling, src/FantasyBasketball.Infrastructure/Persistence/Repositories/ModelVersionRepository.cs, tools/modeling]
test_paths: [tests/FantasyBasketball.Domain.Tests/Modeling, tests/FantasyBasketball.IntegrationTests/Persistence/ModelVersionTests.cs]
depends_on: [persistence_contract.md, backtest_contract.md]
status: partial
last_updated: 2026-10-01
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Every fitted parameter set is an immutable model_version row with its training seasons and metrics.
  - At most one version per model is active, enforced by a unique partial index.
  - C# goldens recompute fixed cases from stored parameters and match Python to 1e-6.
---

# Responsibility

Owns the boundary between offline fitting and online serving, per
[ADR-001](../../../docs/design/adr-001-offline-python-fitting.md). Python fits;
C# applies. Neither side reimplements the other's half.

# The registry

Table `model_version`: `model_name`, `version`, `fitted_at`,
`train_season_end_years`, `parameters` (jsonb), `metrics` (jsonb),
`card_markdown`, `is_active`. Rows are inserted inactive and never edited,
except `is_active`, which `ActivateAsync` swaps in one transaction. Rollback is
activating an older version; nothing is deleted.

# Parameters

Each model's `parameters` JSON has one documented shape, listed here as its
model lands. A model whose parameters fail its shape is not activated.

| Model | Fitted by | Parameters |
|---|---|---|
| `heat-prior` | `tools/modeling/heat_prior.py` | `recentGames`, `minBaselineGames`, `maxBaselineGames`, `minBaselineMinutes`, `nu`, `tauRelative`, `effectFloorPoints`, `effectFloorSd`, `cvByMinutes[{minutesFrom, minutesTo, cv}]` — read by `HeatPriorParameters.Parse` |
| `projection-rates` | `tools/modeling/projection_model.py` | `ageCenter`, `minHistoryMinutes`, `groupOf{position: group}`, `stats{STAT: {weights[3], kappa, mu{G,W,B,U}, alpha, beta, phi, tau}}` for the 13 modelled stats — read by `ProjectionRateParameters.Parse`; REB and PTS are derived |
| `projection-minutes` | `tools/modeling/minutes_model.py` | `ageCenter`, `maxMinutes`, `minHistoryGames`, `rotationFrom`, `starterFrom`, `weights[3]`, `mu`, `kappa`, `delta{bench, rotation, starter, none}`, `beta`, `sigma`, `tau` — read by `MinutesModelParameters.Parse` |
| `projection-availability` | `tools/modeling/availability_model.py` | `ageCenter`, `mpgCenter`, `fullSeason`, `weights[3]`, `a`, `b`, `c`, `phi` — read by `AvailabilityModelParameters.Parse`; the result is a `BetaBinomial(fullSeason, alpha, beta)` |
| `projection-covariance` | `tools/modeling/projection_covariance.py` | `stats` (the 13 modelled stats, in `HierarchicalProjector.ModelledStats` order), `scale`, `correlation{G,W,B,U: 13×13}` — read by `StatCovarianceParameters.Parse`; also needs the active rate and minutes parameters |
| `opponent-choice` | `tools/modeling/choice_model.py` | `roundGroups[]` (first round of each group, starting at 1), `lambdas[]` (one per group), `eta`, `candidates` — read by `OpponentChoiceParameters.Parse`; fitted on public draft logs by maximum likelihood |

# Goldens

`tools/modeling/goldens/<model>.json` holds fixed inputs, the parameters, and
Python's outputs. A C# test per model recomputes the outputs from the same
parameters and must agree to 1e-6. Disagreement fails the gate, so a drift
between the two implementations cannot ship.
