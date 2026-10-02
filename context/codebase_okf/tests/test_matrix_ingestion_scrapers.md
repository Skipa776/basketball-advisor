---
type: test_matrix
title: Test Matrix — Ingestion, Identity, Scrapers, Workers
description: Required cases for provider contracts, the HTTP pipeline, name resolution, parsers, and background refresh.
tags: [tests, ingestion, scrapers, matrix]
source_paths: [src/FantasyBasketball.Infrastructure]
test_paths: [tests/FantasyBasketball.IntegrationTests]
depends_on: [required_gates.md, ../safety/scraping_policy.md, ../contracts/player_identity_contract.md]
status: partial
last_updated: 2026-09-26
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
| `I-14` | balldontlie game with `datetime` null but `date` set (all 11 games on 2022-12-02) | Imported at noon US Eastern on that date, keeping its Eastern date; the run no longer fails | ✅ |

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
| `S-12` | Any test in the suite | `NoNetworkHandler` throws on any real send, and no test file constructs an egress-capable handler without the `s12-allow: loopback self-host` marker | ✅ |
| `S-13` | Parse the same fixture twice | Identical output; parser is pure | ✅ |
| `S-14` | Fixture with a renamed or missing column | Throws a parse error naming the column; **never a row of zeros** | ✅ |
| — | `REB = OREB + DREB` on every fixture row | Identity holds, or the parse fails | ✅ |

# Background workers (`W-`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `W-01` | A worker run throws | Caught, logged, failed run recorded, **next run still executes** | ✅ |
| `W-02` | Host shutdown requested | Worker stops promptly on the token | ✅ |
| `W-03` | Successive worker iterations | Fresh scoped provider and `DbContext` per run | ✅ |
| `W-04` | **Start the import queue, then stop it** | `ExecuteTask` reaches `RanToCompletion` — a clean stop is never reported as a failed background service | ✅ |

`W-04` asserts on `ExecuteTask` rather than on `StopAsync`, because `StopAsync`
awaits with `Task.WhenAny` and so swallows the fault. The host is what observes
the task and logs `BackgroundService failed` plus a critical `StopHost` entry, so
the task is the only place the thing a user actually sees is visible.

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

# League import (`L-01`–`L-11`) — R16

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
| `L-11` | `GetLatestDraftAsync` against the draft fixtures | 150 picks ordered by `pick_no`, 10 teams, 15 rounds, no `/v1/players/nba` request; empty draft list returns null | ✅ |

# Fixtures

Committed under `tests/FantasyBasketball.IntegrationTests/Fixtures/Html/`, named
`{source}-{page}-{yyyy-MM-dd}.html`, trimmed to the relevant table plus
surrounding structure. Capture procedure:
[add_new_data_source](../tasks/add_new_data_source.md).

Provider payloads live alongside as recorded JSON. A Sleeper endpoint with no
committed NBA fixture is not implemented — row `L-07` makes that structural rather
than aspirational. The draft-room pair `sleeper-drafts-2026-09-26.json` and
`sleeper-draft-picks-2026-09-26.json` (a completed 10-team, 15-round snake draft,
150 picks) gates the `/v1/league/{id}/drafts` and `/v1/draft/{id}/picks` reads of
`GetLatestDraftAsync`.

# Verification

`dotnet test --filter "Ingestion|Scrapers|Workers"`, inside the full gate, with
Docker running.

# Current evidence

Rows N-01 through N-05 are implemented in
`PlayerIdentityResolverTests`; N-04 also crosses the real EF transaction and
queryable `DataImportRun` repository in `PersistenceTests`. I-09 and I-13 have
both fake and real PostgreSQL rollback coverage; I-12 proves the second GET
makes no send; S-11 proves two handler instances share one host limiter. A
controlled terminal handler also proves the named factory's resilience path
retries 500s without network access. Recorded balldontlie fixtures implement
I-01 through I-03 and I-10, including cursor pagination; I-06's provider-failure
path returns a queryable failed run, while downstream health-driven confidence
remains. I-07 now has a PostgreSQL round trip for imported teams and games.
Basketball-Reference fixtures implement I-04 and I-05, including canonical
`SeasonStatLine` construction after identity resolution. Its URL builder,
pure parser, and loud schema-drift tests cover the Basketball-Reference portion
of S-10, S-13, S-14, and the rebound-identity row. FantasyPros fixtures and its
exact-path URL gate complete the second scraper's S-10, S-13, and S-14
coverage. I-08 proves both CSV and manual ADP providers produce validated,
provenance-complete values with no scraper registered; the generic import
service resolves and persists every rung as one canonical ADP entity. I-11 and
health-driven half of I-06 are now implemented: failed runs lower draft
confidence and surface `DataQuality` risk evidence. I-11 is covered by
user-created context events whose source is canonical `manual`. W-01 through
W-03 are implemented by a real hosted queue and recurring-worker tests: a
failed ADP run is persisted, later iterations succeed, provider and transaction
scope identities differ on every run, configured cadences bind for all three
registered workers, and shutdown cancels a stagger delay promptly. Post-MVP
box-score and league-import rows remain, so this matrix stays `partial`.

Offline box-score prerequisite (2026-09-21): `BS01_S32` checks the stat identities
against a synthetic full-game fixture. The separate
[test_matrix_box_scores](test_matrix_box_scores.md) covers the parser and atomic
storage. S-30, S-31, S-33 and S-34 still need the resumable worker; no live-site
compatibility or completed historical season is claimed.
Row `L-11` is implemented in `SleeperLeagueProviderTests` against the committed
2026-09-26 draft fixtures: 150 picks ordered by `pick_no`, 10 teams, 15 rounds,
`Nikola Jokić` at pick 1, and no `/v1/players/nba` request.
