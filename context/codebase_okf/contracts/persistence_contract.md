---
type: contract
title: Persistence Contract
description: Entity list, EF Core conventions, migration policy, and the UTC, precision, and immutability rules for stored data.
tags: [contract, persistence, database]
source_paths: [src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests/Persistence]
depends_on: [provenance_contract.md, stat_vocabulary.md]
status: implemented
last_updated: 2026-09-21
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
BoxScoreSnapshot  PlayerGameStat
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

Projection publication (2026-09-20): each new baseline stores the exact
`ObservedStatsId` created beside it. Legacy baselines keep a null link; do not
guess an observation from timestamps. Fantasy values store a UTC `ComputedAt`
and a normalized snapshot of the league scoring profile. Legacy values retain
null publication metadata and must be recalculated before being ranked.
Current reads select the latest timestamp per player and league (ID breaks
equal-timestamp ties deterministically), then require the saved profile to
match current rules. A scoring edit hides outdated values without deleting
history. Decomposition follows value → adjusted → baseline → observed.
It never substitutes another run's baseline or observation.

Values in a full-pool calculation share a `PublicationId`. Once a league has
such a publication, current reads use only its latest complete publication;
players absent from the chosen season/source do not leak in from older runs.

An explicitly requested season/source pool is published in one transaction;
failure or cancellation rolls back all four records. Manual season stats take
precedence over the selected automated source for the same player and season.

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
round-trips the entity. `ProjectionRecords` adds separate observed-stat
references and full baseline rows, with latest-observed and baseline-id reads;
repository serialization rounds projection decimals half-away-from-zero to
four places and PostgreSQL tests migrate and round-trip them. `ContextPipeline`
adds context events and impacts with review/override audit fields, adjusted
projections with their immutable baseline and event references, and league-
scoped fantasy values. A PostgreSQL 17 test migrates from empty and round-trips
all four new record types. `DraftSessionState` completes persisted snake-draft
state needed to reconstruct picks, and `RecommendationPersistence` stores every
recommendation with ordered structured evidence and string enums. PostgreSQL
round-trip tests now cover every entity in this contract.
`IdentityAndNullableOwnership` begins the required auth retrofit without
rewriting history: Identity shares this context, and every owned table gains a
nullable user foreign key so pre-auth data can be claimed before the later
non-null migration.

## Completed box-score snapshots — 2026-09-21

The forward `CompletedBoxScores` migration adds two shared NBA-reference tables;
existing games and season observations are not backfilled into invented samples.
[box_score_storage_contract](box_score_storage_contract.md) owns their shape and
selection rules. Both tables are append-only in `FantasyDbContext`. Foreign keys
to games, snapshots and canonical players restrict deletion. A database check
requires null stats exactly for non-appearances; played zero remains a stat line.
Counting-stat JSON is rounded to four decimals and ratios are derived on read.

Snapshot identity is unique `(game_id, source, parser_version, raw_record_hash,
phase)`; player rows are unique `(snapshot_id, player_id)`. Latest reads use the
season/source/game/fetched-time index and player rows have a player-ID index.
A complete graph is written by one transactional SaveChanges. Duplicate races
become no-ops only for the snapshot identity constraint; other failures roll back
and detach the attempted graph. Corrections append whole pages and reads select
the latest page before date/phase filtering. There is no user/league ownership or
cross-provider merging in these tables. BS-07–BS-10 verify the migration and rules
against disposable PostgreSQL 17.
