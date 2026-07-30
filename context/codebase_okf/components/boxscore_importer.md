---
type: component
title: Box-Score Importer
description: The rate-limited historical importer that unblocks rolling windows, and why it can never be an interactive request.
tags: [component, ingestion, scrapers]
source_paths: [src/FantasyBasketball.Infrastructure/Scrapers/BasketballReference, src/FantasyBasketball.Infrastructure/Workers/BoxScoreImportWorker.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Scrapers]
depends_on: [../safety/scraping_policy.md, scrapers.md, ../contracts/rolling_window_contract.md]
status: planned
last_updated: 2026-07-29
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
The permitted path is `/boxscores/{yyyymmdd}0{TEAM}.html` — one page per game,
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
