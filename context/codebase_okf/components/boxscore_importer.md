---
type: component
title: Box-Score Importer
description: The rate-limited historical importer that unblocks rolling windows, and why it can never be an interactive request.
tags: [component, ingestion, scrapers]
source_paths: [src/FantasyBasketball.Infrastructure/Scrapers/BasketballReference, src/FantasyBasketball.Infrastructure/Scrapers/BasketballReference/BoxScoreImporter.cs, src/FantasyBasketball.Infrastructure/Workers/ImportJobQueue.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Scrapers, tests/FantasyBasketball.IntegrationTests/Persistence/BoxScoreImportTests.cs]
depends_on: [../safety/scraping_policy.md, scrapers.md, ../contracts/rolling_window_contract.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: high
done_criteria:
  - Per-game stat lines exist for a full season without any disallowed request.
  - The import resumes from where it stopped and never re-fetches a completed game.
---

# Responsibility

Owns per-game stat lines — the input every trend, rolling window, and back-test
needs, and the one the MVP deliberately did without.

# The constraint that shapes everything

`*/gamelog/` is **disallowed** by Basketball-Reference's robots directives
([scraping_policy](../safety/scraping_policy.md)), and it is the obvious source.
The proposed path is `/boxscores/{yyyymmdd}0{TEAM}.html` — one page per game,
roughly **1230 games per season**, at the self-imposed 6 requests/minute ceiling:

```text
1230 pages ÷ 6 per minute ≈ 3.5 hours per season
```

That number is the design. A box-score import is a **long-running, resumable,
rate-limited background job** and can never be an interactive request. Any design
that makes a user wait on it is wrong, and any design that speeds it up by raising
the request rate trades the data source for a ban.

# Design

- `BoxScoreImportWorker` walks the schedule (already imported from balldontlie)
  and fetches only games marked complete and not yet imported.
- **Progress is a database fact, not worker state.** Each game's import is one
  transaction; the worker restarts by querying what is missing. A restart mid-season
  costs one page, not one season.
- `BoxScoreParser` is a pure function of HTML, per
  [scrapers](scrapers.md), with committed fixtures.
- Import order is **most recent season first**, then backwards. Recent games serve
  trends, which is the user-facing feature; older seasons serve the back-test,
  which can wait.
- Progress and ETA are surfaced on the Data Sources page — a job with hours of
  latency and no visible progress reads as broken.

# Invariants

- **The URL builder cannot construct a `/gamelog/` path.** *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row S-10, extended to the box-score builder.*
- **The importer shares the global 6/min limiter** and does not get its own
  budget. *Check: row S-11 covers all callers.*
- **A completed game is never re-fetched.** *Check: row S-30 runs the worker
  twice and asserts zero requests on the second pass.*
- **A mid-season restart resumes**, losing at most one game. *Check: row S-31.*
- **Per-game rows satisfy the stat vocabulary identities** (`REB = OREB + DREB`,
  ratios in `0..1`). *Check: row S-32.*
- **A failed page fails that game only** — the run continues and records it.
  *Check: row S-33.*
- **Progress is queryable while running.** *Check: row S-34.*

# Change procedure

A Basketball-Reference box-score redesign: fresh fixture, fix the parser, bump
`ParserVersion`. Never widen the allowlist to reach an easier page.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md),
rows S-10, S-11, S-30 through S-34.

## Offline prerequisite — 2026-09-21

The pure parser and atomic snapshot repository are implemented separately under
[box_score_storage_contract](../contracts/box_score_storage_contract.md), with
[BS-01–BS-10](../tests/test_matrix_box_scores.md) evidence. The synthetic fixture
exercises two teams, comment tables, DNP, played zero and malformed input. It is
not evidence of current live-site compatibility.

The worker, schedule/final-status checks, canonical player resolution, resumable
progress and full-season data remain unimplemented. The stable scraping policy's
path table excludes box scores although its prose describes them as permitted.
No live request or URL-builder expansion is enabled; that conflict needs explicit
policy resolution before any fetching. Existing gamelog rejection is unchanged.
Unknown game phase never silently becomes regular season.

## Owner-authorized importer — 2026-09-23

The owner resolved the path-table conflict: `scraping_policy` now lists
`/boxscores/{yyyymmdd}0{TEAM}.html`. `BbrefUrlBuilder.CreateBoxScoreUri` builds
only that shape; the day index, `pbp/`, `shot-chart/` and `gamelog/` stay
unreachable (row S-10). The parser was checked against one real saved page
(`202511160BOS`: 27 rows, 7 DNP) before any bulk fetch; that page is
third-party content and is not committed.

Rather than a separate worker, the import is an owner job on the existing queue:
`POST /api/imports/box-scores {from, to}` → `ImportJobKind.BoxScores` →
`BoxScoreImporter`. It lists stored balldontlie games with status `Final` whose
US-Eastern date falls in the range, skips any game with a stored
Basketball-Reference snapshot (S-30/S-31: progress is the database), maps team
codes (`BKN→BRK`, `CHA→CHO`, `PHX→PHO`), resolves players through the existing
identity resolver (unresolved rows are counted as pending matches) and stores
one atomic snapshot per game. A failed page fails that game only; the run
records every failed game id (S-33). Progress is logged per game and the run is
visible as `Running` in the queue; a per-game counter in the run list (S-34) is
not built.

Phase is never inferred: the stored schedule does not keep balldontlie's
`postseason` flag, so the owner request asserts the range is regular season.
The NBA Cup championship game does not count toward regular-season statistics
and cannot be told apart in the stored schedule; exclude its date from ranges.

Evidence: `S30_S33_box_score_import_skips_stored_games_and_isolates_a_failed_page`
(Testcontainers PostgreSQL, fixture HTML, fake HTTP), `S10_box_score_builder_allows_only_dated_game_pages`,
`Team_codes_map_between_balldontlie_and_basketball_reference`.

## Nightly refresh — 2026-09-23

`BoxScoreRefreshWorker` enqueues the last `LookbackDays` (default 3) completed
days as an `ImportJobKind.BoxScores` job once a day. It is **off by default** and
cannot be enabled without the owner-set `RegularSeasonStart`/`RegularSeasonEnd`
(the phase guard: the stored schedule cannot tell preseason, play-in or playoff
games apart). `ExcludedDates` removes non-stat days inside the window, such as the
NBA Cup final. Evidence: `Nightly_box_scores_stay_inside_the_owner_regular_season_window`,
and U11 now runs it with a throwing user context.

A schedule re-import now updates an already-stored game's tip-off, status, score
and provenance. Before this, games imported days ahead as "Scheduled" never
became "Final", so the nightly import would have found nothing. Evidence:
`Schedule_reimport_moves_a_scheduled_game_to_final_and_skips_unchanged_games`.

Enable for a season (example for 2026-27; confirm the dates when the NBA
publishes them):

```bash
export RefreshWorkers__BoxScores__Enabled=true
export RefreshWorkers__BoxScores__RegularSeasonStart=2026-10-20
export RefreshWorkers__BoxScores__RegularSeasonEnd=2027-04-11
export RefreshWorkers__BoxScores__ExcludedDates__0=2026-12-15
```
