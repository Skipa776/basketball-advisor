---
type: contract
title: Completed Box-Score Storage
description: Atomic, provenance-carrying game snapshots for appearance-based analysis and an offline HTML parser boundary.
tags: [contract, ingestion, games, heat]
source_paths: [src/FantasyBasketball.Domain/Trends/CompletedBoxScore.cs, src/FantasyBasketball.Application/Abstractions/IBoxScoreRepository.cs, src/FantasyBasketball.Infrastructure/Persistence/Repositories/BoxScoreRepository.cs, src/FantasyBasketball.Infrastructure/Scrapers/BasketballReference/BoxScoreParser.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Scrapers/BoxScoreParserTests.cs, tests/FantasyBasketball.IntegrationTests/Persistence/BoxScoreStorageTests.cs]
depends_on: [player_heat_contract.md, stat_vocabulary.md, provenance_contract.md, persistence_contract.md, player_identity_contract.md, ../safety/scraping_policy.md]
status: implemented
last_updated: 2026-09-21
owners: [engineering]
risk_level: high
---

# Responsibility

Stores completed NBA game observations for descriptive heat, separately from
season projections. This is shared NBA reference data, never private league or
user data. No live fetch, URL-builder expansion, or worker is authorized by this
storage contract. The existing scraping allowlist conflict remains unresolved.

# Snapshot

`CompletedBoxScore` names a canonical `NbaGame.Id`, season end year, provider game
date, page provenance and a non-empty immutable collection of `PlayerGameSample`.
Every sample names the same game, date, season and source, is final, has complete
provenance, and has a distinct canonical player ID. DNP has null statistics;
played appearances retain legitimate zero/negative fantasy scores. No provider
player ID substitutes for a canonical player ID.

The snapshot explicitly carries `NbaGamePhase`, owned in `CompletedBoxScore.cs`:
`Unknown`, `RegularSeason`, `Playoffs`, `Preseason`. Unknown is never inferred to
be regular season. Read callers select a phase; heat callers must explicitly use
regular season. Corrections are selected before phase/date filters so a newer
classification cannot resurrect an outdated eligible snapshot.

`BoxScoreSnapshotRow` stores Guid ID, game ID, season, played-on date and the page
provenance. `PlayerGameStatRow` stores Guid ID, snapshot ID, player ID, DidPlay,
nullable counting-stat JSON and each row's provenance. Foreign keys protect NBA
game/player references. One SaveChanges transaction publishes the whole snapshot;
failure or cancellation leaves no partial snapshot. Both tables are append-only.

Identity is `(game, source, parser version, page hash, phase)`: an unchanged page is an
idempotent no-op, including concurrent submissions. Changed source content writes
a new snapshot. Reads select the latest retrieved snapshot per game for an explicit
source and season (ID breaks timestamp ties), then materialize only that snapshot's
rows. Players removed by a correction never survive from an older snapshot.
Source selection is explicit; sources are not blended or counted twice.

# Pure parser

The first adapter accepts saved HTML only. It expects two distinct
`box-{TEAM}-game-basic` tables, including comment-wrapped tables, and validates
the page's canonical game ID against the caller's expected provider game ID.
Quarter/advanced tables, repeated headers and team totals are not player rows.
The canonical counting-column map owns the `data-stat` translation. Missing,
duplicate, malformed or negative required cells fail loudly. Minutes parse as
`minutes:seconds`; seconds must be below 60. Counts must be whole numbers.
Rebounds must add up and made shots cannot exceed attempts. Ratios are derived.
Recognized non-appearance reasons produce DNP, not a zero line; unknown reasons
fail rather than silently excluding an appearance. Duplicate identities fail.

The page SHA-256 is computed from the supplied HTML before parsing. The parser
returns external identities, stat lines and the page hash; it neither fetches
URLs nor resolves players nor writes a database. A future importer must use the
existing identity resolver, record unresolved identities, and confirm the game
is final and belongs to the requested regular-season schedule before publication.
The initial synthetic fixtures verify parser behavior, not current live-site
compatibility. A verified saved real page is required before enabling fetching.

# Required verification

BS-01 normal/comment tables and deterministic hash; BS-02 DNP versus played zero;
BS-03 required-column/schema failures; BS-04 invalid counts, minutes, rebounds and
shooting; BS-05 duplicate identity/table or wrong page; BS-06 snapshot validation;
BS-07 migrate/round-trip with no user context; BS-08 duplicate/concurrent no-op;
BS-09 corrected snapshot and season/source isolation; BS-10 rollback, cancellation
and append-only enforcement. Heat integration consumes these stored samples; it
does not substitute season statistics when they are absent.
