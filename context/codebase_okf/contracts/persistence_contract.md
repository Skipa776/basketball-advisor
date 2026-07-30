---
type: contract
title: Persistence Contract
description: Entity list, EF Core conventions, migration policy, and the UTC, precision, and immutability rules for stored data.
tags: [contract, persistence, database]
source_paths: [src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests/Persistence]
depends_on: [provenance_contract.md, stat_vocabulary.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Migrations apply cleanly to an empty Postgres 17 and round-trip every entity.
  - Immutable tables have no update path in any repository.
---

# Responsibility

Owns the database shape and the rules that keep stored data trustworthy. Engine
and version are in [`stack_config.toml`](../../../stack_config.toml); type shapes
are in [`ARCHITECTURE.md`](../../../ARCHITECTURE.md).

# Entities

```text
Player  NbaTeam  ExternalPlayerIdentity  PendingIdentityMatch
SeasonStatLine  NbaGame  AdpEntry
FantasyLeague  ScoringRule  RosterSlot
DraftSession  DraftPick
ObservedStats  BaselineProjection  AdjustedProjection  FantasyValue
ContextEvent  PlayerContextImpact
Recommendation  RecommendationEvidence
DataImportRun
```

`FantasyRoster`, `FantasyTransaction`, `NewsItem`, and `ScheduleSnapshot` from
the design doc's entity list are **post-MVP** — they belong to league import,
streaming, and news ingestion. Do not create empty tables for them.

# Conventions

- `snake_case` tables and columns via a naming convention applied in
  `OnModelCreating`, not per-entity attributes.
- Primary keys are `Guid` (`uuid`), generated in the application, never
  database-sequential — ids must be assignable before persistence so domain
  objects are valid on construction.
- All timestamps are `timestamptz`, always UTC, always from an injected
  `TimeProvider`.
- Stat and projection values are `numeric(10,4)`. Scoring rule values are
  `numeric(8,4)`. Never `float`/`double`.
- `StatLine` dictionaries persist as `jsonb`, keyed by the `StatKey` name from
  [stat_vocabulary](stat_vocabulary.md).
- Provenance is an owned type on every entity that can originate externally,
  with all columns `NOT NULL`.
- Every enum is stored **as its string name**, not its ordinal — a reordered
  enum must not silently reinterpret existing rows.

# Indexes

Required, because each backs a hot path:

| Table | Index |
|---|---|
| `player` | `normalized_name` |
| `external_player_identity` | unique `(provider, external_id)` |
| `season_stat_line` | unique `(player_id, season_end_year, source)` |
| `draft_pick` | unique `(draft_session_id, pick_number)` |
| `adjusted_projection` | `(player_id, computed_at desc)` |
| `context_event` | `(effective_from, expected_expiration)` |
| `data_import_run` | `(source, started_at desc)` |

# Migration policy

- Every schema change is an EF Core migration, committed with the code that
  needs it. No `EnsureCreated`, ever — it silently diverges from migrations.
- Migrations are forward-only and never edited after being committed.
- Integration tests apply migrations from empty on a Testcontainers instance, so
  a broken migration fails the build rather than a developer's machine.

# Invariants

- **`BaselineProjection`, `ObservedStats`, `DraftPick`, and `DataImportRun` are
  append-only.** Their repositories expose no update method — this is a shape
  guarantee, not a discipline. *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row
  A-04 reflects over the repository interfaces and asserts no update surface;
  row A-05 asserts a direct `SaveChanges` on a modified baseline throws.*
- **Tests never touch a developer database.** Integration tests get a throwaway
  container per class fixture; the connection string is generated, never read
  from developer configuration. *Check: row A-06.*
- **Unique constraints back the identity rules**, so a duplicate provider link
  fails at the database, not only in application logic. *Check: row A-07.*
- **Enums are stored as strings.** *Check: row A-08 reorders an enum in a test
  double and asserts stored rows still read correctly.*
- **No `DateTime.Now` or ambient `UtcNow`.** *Check: the forbidden-pattern scan.*
- **Deleting a league cascades to its draft sessions and picks; deleting a
  player never cascades to stat history** — history outlives roster churn.
  *Check: row A-09.*

# Change procedure

Adding an entity: shape in `ARCHITECTURE.md`, configuration, migration, index if
it has a hot path, repository interface in Application, and its row in this
file — one commit.

# Verification

`test_matrix_api_persistence.md`, rows A-04 through A-09, plus a migrate-from-empty
test.

# Implementation evidence

The initial schema plus the forward `IdentityResolution` and `ImportRuns`
migrations now cover the identity entities and immutable import history.
Provider links are unique both by `(provider, external_id)` and by
`(player_id, provider)`, which makes the N-05 append-only conflict rule a
database invariant. `ScheduleSource` adds canonical NBA-team uniqueness,
auditable team-source rows, and provenance-complete games with unique
`(source, external_id)` identity. The forward `AdpEntries` migration adds
player-linked ADP values, optional variance, a latest-by-player index, and
provenance-complete storage; a Testcontainers test migrates from empty and
round-trips the entity. The contract remains `partial` until every entity in
the list above is present and round-tripped.
