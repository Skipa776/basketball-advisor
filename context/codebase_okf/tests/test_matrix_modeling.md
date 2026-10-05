---
type: test_matrix
title: Test Matrix — Offline Model Parameters
description: Required cases for the model-version registry and the C#/Python parameter goldens.
tags: [tests, modeling, persistence, matrix]
source_paths: [src/FantasyBasketball.Domain/Modeling, src/FantasyBasketball.Domain/Projections/HierarchicalProjector.cs, src/FantasyBasketball.Domain/Projections/MinutesModel.cs, src/FantasyBasketball.Domain/Projections/AvailabilityModel.cs, src/FantasyBasketball.Domain/Statistics/BetaBinomial.cs, src/FantasyBasketball.Domain/Projections/StatCovariance.cs, src/FantasyBasketball.Domain/Projections/ProjectionDistribution.cs, src/FantasyBasketball.Infrastructure/Persistence/Repositories/ModelVersionRepository.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Modeling, tests/FantasyBasketball.Domain.Tests/Projections/HierarchicalProjectorTests.cs, tests/FantasyBasketball.Domain.Tests/Projections/MinutesModelTests.cs, tests/FantasyBasketball.Domain.Tests/Projections/AvailabilityModelTests.cs, tests/FantasyBasketball.Domain.Tests/Projections/ProjectionDistributionTests.cs, tests/FantasyBasketball.IntegrationTests/Persistence/ModelVersionTests.cs]
depends_on: [required_gates.md, ../contracts/model_params_contract.md]
status: partial
last_updated: 2026-10-01
owners: [engineering]
---

# Responsibility

Gates [model_params_contract](../contracts/model_params_contract.md).

# Registry (`MV-01`–`MV-09`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `MV-01` | Add a version, activate it | `GetActiveAsync` returns it with every field intact (jsonb compared parsed) | ✅ |
| `MV-02` | Activate a second version of the same model | Exactly one active row; the first is inactive | ✅ |
| `MV-03` | Activate a version that does not exist | `InvalidOperationException`; the previously active version stays active | ✅ |
| `MV-04` | List a model's versions | Newest `FittedAt` first | ✅ |
| `MV-05` | Blank model name or version | `ArgumentException` | ✅ |
| `MV-06` | No training seasons | `ArgumentException` | ✅ |
| `MV-07` | A training season before 1947 | `ArgumentException` | ✅ |
| `MV-08` | Blank parameters or metrics JSON | `ArgumentException` | ✅ |
| `MV-09` | Season 1947 and every field set | Accepted; fields kept as given | ✅ |

# Goldens (`MG-01`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `MG-01` | Every `tools/modeling/goldens/*.json` | C# recomputes each expected output from the stored parameters within 1e-6 | ✅ |

# Hierarchical projector (`HP-01`–`HP-04`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `HP-01` | No history with 100+ minutes in the last three seasons | The position group's prior (group `U` when the position is unknown) times the age term; no age means exp(0) | ✅ |
| `HP-02` | Any projection | `REB = OREB + DREB` and `PTS = 2·FGM + FG3M + FTM` exactly | ✅ |
| `HP-03` | Seasons at lags 1, 2, 3 (under 100 min) and 4 | Lags 1 and 2 weighted 1 and w₂ with the κ prior; the short and the lag-4 seasons are ignored; age carried forward by its lag | ✅ |
| `HP-04` | Parameters missing a modelled stat | `ArgumentException` naming the stat | ✅ |

# Minutes model (`MM-01`–`MM-06`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `MM-01` | No season with 5+ games in the last three | League mean μ plus the `none` shift and the age term | ✅ |
| `MM-02` | Last season at 17.9, 18 and 28 minutes; no last season but an older one | Bench, rotation and starter shifts at those edges; `none` without a last season | ✅ |
| `MM-03` | A projection above 42 or below 0 | Clamped to `maxMinutes` and 0 | ✅ |
| `MM-04` | Seasons at lags 1, 2, 3 (under 5 games) and 4 | Games × recency weights with κ shrinkage; the short and lag-4 seasons are ignored; age carried forward by its lag | ✅ |
| `MM-05` | Parameters missing a role shift | `ArgumentException` naming the roles | ✅ |
| `MM-06` | Sat out last season (seasons at lags 2 and 3) | Recency counts from the newest season played: lags 2 and 3 take the lag-1 and lag-2 weights; only the `none` shift differs from the same seasons a year later | ✅ |

# Availability model (`AV-01`–`AV-05`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `AV-01` | `BetaBinomial(82, 2.5, 0.8)` | Mean 62.1212 and variance 298.742 (closed form); quantiles 0.1 / 0.5 / 0.9 = 36 / 67 / 81 as scipy; 0 and 1 map to 0 and 82 | ✅ |
| `AV-02` | `LogGamma` at 0.5, 10 and 0.1 | Within 1e-12 of log √π, log 9!, and `math.lgamma(0.1)` | ✅ |
| `AV-03` | Two history seasons; no history | Alpha and beta add weighted games played and missed to φm and φ(1 − m); no history is the prior alone | ✅ |
| `AV-04` | An 84-game line, a lag-5 line and a 0-game line | Games capped at the season's length; the lag-5 and 0-game lines are ignored | ✅ |
| `AV-05` | Parameters with one weight | `ArgumentException` | ✅ |

# Covariance and distributions (`SC-01`–`SC-04`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `SC-01` | No rate noise, minutes variance 4 | Every stat pair covaries by r_s r_t · 4; MIN has variance 4 and covaries with a stat by r_s · 4 | ✅ |
| `SC-02` | Fantasy points with PTS, FGM and REB rules | PTS folds to 2·FGM + FG3M + FTM and REB to OREB + DREB, so FGM's effective weight is 4; the mean uses the per-game means | ✅ |
| `SC-03` | Season total | Mean μ·E[G]; variance σ²(E[G]² + Var G) + μ² Var G | ✅ |
| `SC-04` | Covariance parameters with stats out of order, or a non-square correlation | `ArgumentException` | ✅ |

# Opponent choice (`OC-01`–`OC-03`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `OC-01` | λ 1 in round 1 with ADPs 1 and e, the second filling a need (η 1); λ 2 in round 5 with ADPs 2 and 4 | 0.5 / 0.5; 0.8 / 0.2 (softmax of −λ log ADP + η·need); probabilities sum to 1 | ✅ |
| `OC-02` | Uniform 0.79 and 0.81 against 0.8 / 0.2; an ADP of 0; round groups not starting at 1 | Candidates 0 and 1 (inverse CDF); both bad inputs throw `ArgumentException` | ✅ |
| `OC-03` | A mock draft with an active `opponent-choice` model (λ 40) | Simulated opponents draw from the model, taking the lowest ADPs in order; without a model the ADP-jitter heuristic still applies | ✅ |

# Shared statistics (`ND-01`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `ND-01` | `NormalDistribution.Cdf` at 0, 1, −1.96, 2.5 | Within 2e-7 of published values (Abramowitz–Stegun 7.1.26 bound 1.5e-7) | ✅ |
