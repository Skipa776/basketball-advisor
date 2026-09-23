---
type: test_matrix
title: Test Matrix — Architecture, Persistence, API
description: Required cases for the dependency boundaries, database rules, HTTP envelope, lifecycle conflicts, and the UI's decomposition display.
tags: [tests, api, persistence, matrix]
source_paths: [src/FantasyBasketball.Api, src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests]
depends_on: [required_gates.md, ../contracts/persistence_contract.md, ../contracts/api_surface.md]
status: partial
last_updated: 2026-09-23
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

# API (`A-10`–`A-15`, `A-18`, `A-19`, `A-35`)

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
| `A-35` | **`GET` each list route with no query string** | `200` with the documented defaults (`page` 1, `limit` 50) — not `400` | ✅ |

# UI (`A-20`, `A-21`, `A-36`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-20` | Render a player projection | Shows baseline, adjustment, and final separately — never the final number alone (R8) | ✅ |
| `A-21` | Render a number affected by an unverified event | Visibly marked as unverified | ⏳ React re-coverage pending (Blazor render test retired 2026-09-23) |
| `A-36` | **Create a league, then request `/league` and `/draft` again** | Both show it: the summary carries the ID, the draft page offers it as an option | ✅ |

`A-35` and `A-36` are both "the second request" rows. Every route in `A-14`'s sweep
was called the way its author called it — `?page=1&limit=50`, or in the same render
that created the data. The user's second visit is a different request, and until
these rows existed nothing tested it: `/api/players` answered `400` and My League
showed its empty state while the league sat in the database.

# Self-host hardening and distribution (`A-22`–`A-34`) — R21, R22

| ID | Case | Expected | Required |
|---|---|---|---|
| `A-22` | Boot with default configuration and no secrets | Serves on loopback; app is usable | ✅ |
| `A-23` | Non-loopback bind with no TLS | Fails at boot, naming the setting | ✅ |
| `A-24` | Inspect the built image | Non-root user, read-only root filesystem, no added capabilities | ✅ |
| `A-25` | Parse the shipped `compose.yaml` | Postgres port is not published to the host | ✅ |
| `A-26` | Demo data rendered anywhere | Labelled fictional; opt-in via flag | ✅ |
| `A-27` | Apply migrations to a populated volume | Forward-only, no data loss; seeded rows survive | ✅ |
| `A-28` | Request `/metrics` from off-host with defaults | Not reachable | ✅ |
| `A-29` | **`docker compose up` from a clean clone, no keys** | Working app with demo data; `/health/ready` returns healthy | ✅ |
| `A-30` | CI workflow | Invokes `scripts/gate.sh` — not a reimplementation of it | ✅ |
| `A-31` | Built image | Multi-arch (`amd64`, `arm64`); no SDK in the runtime layer | ✅ |
| `A-32` | Pending migrations | `/health/ready` fails; `/health/live` still succeeds | ✅ |
| `A-33` | Scrape requests consumed against the ceiling | Exported as a metric | ✅ |
| `A-34` | README configuration table vs the options classes | Match, by reflection — documentation drift caught by a test | ✅ |

`A-29` is the whole distribution claim in one row: a stranger, one command, no keys.
If it fails, nothing else about "publishable" is true.

`A-34` exists because a README that documents an intention rather than the artifact
is the most common defect in self-hosted software, and it is trivially checkable.

# Verification

`dotnet test --filter "Architecture|Persistence|Api|Configuration"`, inside the full
gate, with Docker running. `A-29` and `A-31` require a Docker builder and run in CI.

# Current evidence

A-01 through A-21 are implemented. Architecture reflection, append-only
repository shape, migration-from-empty, PostgreSQL constraints, and dependency
direction cover A-01 through A-09, A-16, A-17, and A-18. A local-loopback
Kestrel test over a throwaway PostgreSQL 17 database covers the universal
envelope, sanitized errors, validation, every route family, idempotent picks,
last-pick undo, context verification conflicts, startup validation, and
request-token handler signatures. The same real host renders the Players page
against a persisted adjusted projection and asserts baseline, adjustment, final,
and the unverified-context marker for A-20 and A-21. Distribution rows A-22
onward belong to E12, so this matrix remains `partial`.

React bridge coverage additionally checks explicit ESPN setup against an independent
53-point scoring golden, an eleven-team league, list defaults/invalid pagination,
and draft reload returning the persisted picks.

## Blazor retired — 2026-09-23

`A-20` and `A-36` are now evidenced by the React journey
(`HP05_A20_A36_…` → `workspace.mjs`): the player detail shows Baseline,
Adjusted and Final separately, and a saved league/draft survives a reload.
`A-21` (visible unverified marking) has no React assertion yet.
