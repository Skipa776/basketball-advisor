# E06 — Box-score importer and trend engine

**Requirements:** R11 · **Depends on:** E04

Per-game stat lines and the riser/faller engine. The epic that makes the app useful
after draft night — and the one whose shape is dictated by a robots directive.

## Pre-flight

E04 landed green · `scripts/gate.sh` green · at least one season's schedule imported,
because the importer walks it.

**Start the import early.** At 6 requests/minute, one season is roughly 3.5 hours of
wall clock. Kick off the importer as soon as it works and build the trend engine while
it runs — the trend tests use committed fixtures, not the live import.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E06 in this repository: the box-score importer and the trend engine.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R11),
   ARCHITECTURE.md post-MVP section, and context/AGENT_CONTEXT_INDEX.md. Then
   read, in full:
     context/codebase_okf/safety/scraping_policy.md      (read this FIRST)
     context/codebase_okf/components/boxscore_importer.md
     context/codebase_okf/contracts/rolling_window_contract.md
     context/codebase_okf/components/trend_engine.md
     context/codebase_okf/components/scrapers.md
     context/codebase_okf/tests/test_matrix_trends.md
     context/codebase_okf/tests/test_matrix_ingestion_scrapers.md (rows S-30 to S-34)

2. The constraint that shapes this epic: Basketball-Reference's robots directives
   DISALLOW */gamelog/, which is the obvious source for per-game data. The
   permitted path is /boxscores/{yyyymmdd}0{TEAM}.html — about 1230 pages per
   season, which at the 6/min ceiling is roughly 3.5 hours.

   That number is the design. The importer is a long-running, resumable,
   rate-limited BackgroundService and can never be an interactive request. Do not
   raise the request rate to make it faster: that trades the data source for a
   ban.

3. Scope, in order:
   a. BoxScoreScraper + BoxScoreParser, with an allowlist-enforcing URL builder
      that throws on any */gamelog/-shaped path, and committed HTML fixtures.
   b. BoxScoreImportWorker: walks the schedule, fetches only completed games not
      yet imported, one transaction per game. PROGRESS IS A DATABASE FACT, not
      worker state — restart by querying what is missing, so a mid-season restart
      costs one page rather than one season. Most recent season first, then
      backwards. Progress and ETA surfaced on the Data Sources page.
   c. RollingWindow materialization for all six spans.
   d. The three-way production decomposition, exactly as the contract specifies.
      FromMinutes + FromUsage + FromEfficiency must equal the total change
      EXACTLY — the identity is algebraic, not empirical.
   e. Sustainability classification from the decomposition and sample weight —
      never from the size of the production change.
   f. TrendScore, risers/fallers, and free-agent ranking as QUERIES over windows,
      never a stored leaderboard.
   g. TrendRefreshWorker rebuilding windows after each import batch, not on a
      timer.

4. Required test rows: T-01 to T-06, T-10 to T-14, S-30 to S-34. Row T-01
   reproduces the contract's worked example to 4 decimal places; row T-02 asserts
   the identity within 1e-9 across 200 generated pairs; row T-03 gives two players
   the same total change with opposite decompositions and asserts opposite labels.

5. Commit after each of a-g. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

6. Budget ladder: (a) scraper + parser + fixtures; (b) the worker; (c) windows;
   (d) the decomposition and score; (e) rankings and the refresh worker. The
   decomposition without the importer is testable from fixtures and still worth
   landing.

7. Non-negotiable: never request a disallowed path; share the global 6/min limiter
   rather than taking a separate budget; no test touches the network; the
   decomposition lives in Domain and takes no repository; no placeholder files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated with importer progress and the next step, commit.

Final report: implemented / partial / missing, how many games are imported and for
which seasons, the T-01 and T-02 results, and the one next step. Under-claim
rather than over-claim.
```

---

## Exit gate

Rows T-01…T-06, T-10…T-14, S-30…S-34 green, at least one season of per-game data
imported with no disallowed request made, and a risers list that labels an
efficiency-driven spike as such.
