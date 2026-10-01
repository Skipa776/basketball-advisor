---
type: test_matrix
title: Test Matrix — Offline Model Parameters
description: Required cases for the model-version registry and the C#/Python parameter goldens.
tags: [tests, modeling, persistence, matrix]
source_paths: [src/FantasyBasketball.Domain/Modeling, src/FantasyBasketball.Infrastructure/Persistence/Repositories/ModelVersionRepository.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Modeling, tests/FantasyBasketball.IntegrationTests/Persistence/ModelVersionTests.cs]
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
