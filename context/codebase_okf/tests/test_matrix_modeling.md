---
type: test_matrix
title: Test Matrix — Offline Model Parameters
description: Required cases for the model-version registry and the C#/Python parameter goldens.
tags: [tests, modeling, persistence, matrix]
source_paths: [src/FantasyBasketball.Domain/Modeling, src/FantasyBasketball.Domain/Projections/HierarchicalProjector.cs, src/FantasyBasketball.Domain/Projections/MinutesModel.cs, src/FantasyBasketball.Infrastructure/Persistence/Repositories/ModelVersionRepository.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Modeling, tests/FantasyBasketball.Domain.Tests/Projections/HierarchicalProjectorTests.cs, tests/FantasyBasketball.Domain.Tests/Projections/MinutesModelTests.cs, tests/FantasyBasketball.IntegrationTests/Persistence/ModelVersionTests.cs]
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

# Minutes model (`MM-01`–`MM-05`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `MM-01` | No season with 5+ games in the last three | League mean μ plus the `none` shift and the age term | ✅ |
| `MM-02` | Last season at 17.9, 18 and 28 minutes; no last season but an older one | Bench, rotation and starter shifts at those edges; `none` without a last season | ✅ |
| `MM-03` | A projection above 42 or below 0 | Clamped to `maxMinutes` and 0 | ✅ |
| `MM-04` | Seasons at lags 1, 2, 3 (under 5 games) and 4 | Games × recency weights with κ shrinkage; the short and lag-4 seasons are ignored; age carried forward by its lag | ✅ |
| `MM-05` | Parameters missing a role shift | `ArgumentException` naming the roles | ✅ |

# Shared statistics (`ND-01`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `ND-01` | `NormalDistribution.Cdf` at 0, 1, −1.96, 2.5 | Within 2e-7 of published values (Abramowitz–Stegun 7.1.26 bound 1.5e-7) | ✅ |
