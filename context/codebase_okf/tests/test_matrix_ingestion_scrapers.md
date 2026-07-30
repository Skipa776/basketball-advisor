---
type: test_matrix
title: Test Matrix — Ingestion, Identity, Scrapers, Workers
description: Required cases for provider contracts, the HTTP pipeline, name resolution, parsers, and background refresh.
tags: [tests, ingestion, scrapers, matrix]
source_paths: [src/FantasyBasketball.Infrastructure]
test_paths: [tests/FantasyBasketball.IntegrationTests]
depends_on: [required_gates.md, ../safety/scraping_policy.md, ../contracts/player_identity_contract.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The cases gating build steps 5 through 9 and 14 — requirements R2, R3, R4.

# Provider contracts and pipeline (`I-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `I-01` | balldontlie teams payload | Maps to canonical `NbaTeam` models | ✅ |
| `I-02` | balldontlie players payload | Maps to `ExternalPlayer` with `ExternalId` preserved | ✅ |
| `I-03` | balldontlie games payload | Maps to `NbaGame` with UTC timestamps | ✅ |
| `I-04` | Basketball-Reference season tables | Map to `SeasonStatLine` per the stat vocabulary | ✅ |
| `I-05` | Advanced page `USG% = 24.5` | Stored as `0.245`; percentage points divided at the boundary | ✅ |
| `I-06` | Provider returns 500 | `DataImportRun` recorded failed; no throw out of the use case; confidence lowered | ✅ |
| `I-07` | Any persisted imported row | Non-null provenance, correct canonical source name | ✅ |
| `I-08` | ADP with the scraper disabled | CSV and manual rungs still produce a value | ✅ |
| `I-09` | Cancel mid-import | Stops promptly; no partial commit | ✅ |
| `I-10` | Re-import an unchanged fixture | `RawRecordHash` is stable | ✅ |
| `I-11` | User-entered roster or event | Provenance recorded with `Source = manual` | ✅ |
| `I-12` | Second request inside the freshness window | Served from cache; zero outbound requests | ✅ |
| `I-13` | Import fails partway | Previously imported data intact; no truncate-then-load | ✅ |

# Identity resolution (`N-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `N-01` | The normalization table in the identity contract | Every row round-trips exactly | ✅ |
| `N-02` | Known `(provider, externalId)` | Tier-1 resolve; no new player created | ✅ |
| `N-03` | Two players sharing a normalized name | `PendingIdentityMatch`; **zero links written** | ✅ |
| `N-04` | Import containing one ambiguous player | Run completes, records the pending count, stays queryable | ✅ |
| `N-05` | Re-import with a conflicting external id | Existing link untouched; pending match created | ✅ |

# Scrapers (`S-10`–`S-14`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `S-10` | Ask each URL builder for a `*/gamelog/` path | Throws; disallowed paths are unreachable by construction | ✅ |
| `S-11` | 20 rapid requests through the pipeline | Elapsed time consistent with the 6/min ceiling; limiter is per host and process-wide | ✅ |
| `S-12` | Any test in the suite | A handler that throws on real send is installed; nothing reaches the network | ✅ |
| `S-13` | Parse the same fixture twice | Identical output; parser is pure | ✅ |
| `S-14` | Fixture with a renamed or missing column | Throws a parse error naming the column; **never a row of zeros** | ✅ |
| — | `REB = OREB + DREB` on every fixture row | Identity holds, or the parse fails | ✅ |

# Background workers (`W-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `W-01` | A worker run throws | Caught, logged, failed run recorded, **next run still executes** | ✅ |
| `W-02` | Host shutdown requested | Worker stops promptly on the token | ✅ |
| `W-03` | Successive worker iterations | Fresh scoped provider and `DbContext` per run | ✅ |

# Box-score importer (`S-30`–`S-34`) — R11

| ID | Case | Expected | Required |
|---|---|---|---|
| `S-30` | Run the worker twice over the same schedule | Zero requests on the second pass — a completed game is never re-fetched | ✅ |
| `S-31` | Kill and restart mid-season | Resumes from the database; at most one game re-fetched | ✅ |
| `S-32` | Every parsed box-score row | Satisfies `REB = OREB + DREB` and ratios in `0..1` | ✅ |
| `S-33` | One page returns malformed HTML | That game fails and is recorded; the run continues | ✅ |
| `S-34` | Query while running | Progress and remaining count are readable | ✅ |

The importer shares the global 6/min limiter (row `S-11`) and its URL builder is
covered by the `/gamelog/` rejection in row `S-10`.

# League import (`L-01`–`L-10`) — R16

| ID | Case | Expected | Required |
|---|---|---|---|
| `L-01` | League with a scoring stat outside `StatKey` | `validation_failed` naming that stat; no league created; **never approximated** | ✅ |
| `L-02` | Import over an existing league | Nothing changes until the diff is confirmed | ✅ |
| `L-03` | Import that would revert a manual override | The override is flagged separately in the diff | ✅ |
| `L-04` | **Every adapter unregistered** | Full MVP flow works through CSV and manual entry | ✅ |
| `L-05` | Yahoo authorization URL | Contains read-only scope and no write scope | ✅ |
| `L-06` | Captured logs and responses across a Yahoo flow | No access or refresh token present | ✅ |
| `L-07` | Sleeper adapter's endpoint list | Equals the set of committed NBA fixtures | ✅ |
| `L-08` | Any snapshot | Carries provenance; unresolved players become pending matches without failing the run | ✅ |
| `L-09` | Scan outside the adapter assemblies | No branch on a provider name | ✅ |
| `L-10` | Generated CSV template through the parser | Round-trips — template and parser share one schema | ✅ |

# Fixtures

Committed under `tests/FantasyBasketball.IntegrationTests/Fixtures/Html/`, named
`{source}-{page}-{yyyy-MM-dd}.html`, trimmed to the relevant table plus
surrounding structure. Capture procedure:
[add_new_data_source](../tasks/add_new_data_source.md).

Provider payloads live alongside as recorded JSON. A Sleeper endpoint with no
committed NBA fixture is not implemented — row `L-07` makes that structural rather
than aspirational.

# Verification

`dotnet test --filter "Ingestion|Scrapers|Workers"`, inside the full gate, with
Docker running.

# Current evidence

Rows N-01 through N-05 are implemented in
`PlayerIdentityResolverTests`; N-04 also crosses the real EF transaction and
queryable `DataImportRun` repository in `PersistenceTests`. The I-, S-, and W-
rows remain pending their corresponding E01 slices, so this matrix remains
`partial`.
