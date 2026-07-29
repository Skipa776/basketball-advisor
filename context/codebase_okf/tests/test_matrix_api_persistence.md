---
type: test_matrix
title: Test Matrix — Architecture, Persistence, API
description: Required cases for the dependency boundaries, database rules, HTTP envelope, lifecycle conflicts, and the UI's decomposition display.
tags: [tests, api, persistence, matrix]
source_paths: [src/FantasyBasketball.Api, src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests]
depends_on: [required_gates.md, ../contracts/persistence_contract.md, ../contracts/api_surface.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The cases gating build steps 1, 4, and 13 — the architectural boundary, the
database rules, and the HTTP contract.

# Architecture (`A-01`–`A-03`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-01` | Reflect over the `Domain` assembly | Referenced assemblies are framework-only: no EF Core, no `HttpClient`, no provider types | ✅ |
| `A-02` | Construct each domain type with invalid input | Throws in the constructor; no `IsValid()` to forget | ✅ |
| `A-03` | Reflect over `Application` | No concrete Infrastructure type referenced | ✅ |

# Persistence (`A-04`–`A-09`, `A-16`, `A-17`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-04` | Reflect over append-only repository interfaces | No update method exists on `BaselineProjection`, `ObservedStats`, `DraftPick`, `DataImportRun` | ✅ |
| `A-05` | Modify a tracked `BaselineProjection` and save | Throws; the baseline cannot be mutated through any path | ✅ |
| `A-06` | Any integration test | Uses a Testcontainers database; connection string is generated, never from developer config | ✅ |
| `A-07` | Insert a duplicate `(provider, external_id)` | Fails at the database, not only in application logic | ✅ |
| `A-08` | Enum reordered in a test double | Existing rows still read correctly — enums stored as strings | ✅ |
| `A-09` | Delete a league / delete a player | League cascades to draft data; player never cascades to stat history | ✅ |
| `A-16` | Apply migrations to an empty database | Succeeds from scratch; no `EnsureCreated` anywhere | ✅ |
| `A-17` | Reflect over repository interfaces | Return domain types; no `IQueryable` leaks outward | ✅ |

# API (`A-10`–`A-15`, `A-18`, `A-19`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-10` | Force an unhandled exception | `internal_error` envelope; body contains no stack trace, SQL, or connection string | ✅ |
| `A-11` | Submit the same pick twice, then a different player for the same pick number | Idempotent, then `conflict`. The live-draft double-click | ✅ |
| `A-12` | Undo a pick that is not the most recent | `conflict` | ✅ |
| `A-13` | Verify an already-verified or rejected event | `conflict`; verification endpoint is the only writer of `Verified` | ✅ |
| `A-14` | Sweep every route, success and failure | Envelope shape on both | ✅ |
| `A-15` | Boot without `BallDontLie:ApiKey` | Startup fails, error names the key | ✅ |
| `A-18` | Inspect DI registration | Only the Api project wires concrete Infrastructure types | ✅ |
| `A-19` | Cancel a request mid-flight | Token propagates; no orphaned work | ✅ |

# UI (`A-20`, `A-21`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-20` | Render a player projection | Shows baseline, adjustment, and final separately — never the final number alone (R8) | ✅ |
| `A-21` | Render a number affected by an unverified event | Visibly marked as unverified | ✅ |

# Verification

`dotnet test --filter "Architecture|Persistence|Api"`, inside the full gate, with
Docker running.
