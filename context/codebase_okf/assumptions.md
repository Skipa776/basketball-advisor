---
type: assumptions
title: Assumptions, Deliberate Defaults, and Current State
description: What is true right now, what was decided on purpose and why, what is knowingly unenforced, and what should trigger a revisit.
tags: [state, decisions, risks]
source_paths: []
test_paths: []
depends_on: [safety/scraping_policy.md, tasks/post_mvp_roadmap.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The bundle's honesty file. Records current state, decisions made without an
upstream source to cite, invariants that are knowingly prose-only, and the
conditions that should make someone revisit each. A resuming agent reads this
second, right after `AGENTS.md`.

# Current state — 2026-07-29

**Build steps 1–3 are implemented.** The repository has the solution skeleton,
central dependency pins and lock files, healthy local PostgreSQL Compose
configuration, and a green executable quality gate. The canonical stat
vocabulary, valid-by-construction league configuration, points scoring, and
category scoring are implemented with all required scoring-matrix cases.

**Build step 4 is partial.** EF Core now has generated forward migration and
snapshot files, explicit per-entity configuration, snake-case PostgreSQL
storage, JSONB stat lines, string enums, numeric precision, uniqueness indexes,
append-only baseline enforcement, and repositories that round-trip leagues,
players, and season stat lines. Tests use isolated Testcontainers PostgreSQL 17
instances and cover migration from empty, duplicate provider identity,
baseline immutability, enum storage, dependency direction, repository shape,
and league/draft cascade versus player-history restriction.

The full gate passes 26 Domain, 1 Application, and 7 integration tests. Domain
line coverage is 87.11%, Application line coverage is 100%, the build has zero
warnings, and OKF validation is clean.

Persistence remains `partial` because the initial migration does not yet contain
the later projection, context, recommendation, import-run, game, ADP, and
pending-identity entities listed by the full persistence contract. The single
next action is to finish step 4's remaining entity/configuration/repository
surface and the exact A-04 append-only interface checks before starting the
identity resolver in step 5.

**Build prerequisites not yet installed on the development machine:**

- **.NET 10 SDK is installed.** SDK 10.0.302 satisfies `global.json` through
  `rollForward: latestFeature`. Verified 2026-07-29.
- Docker 28.5.1 is present — `compose.yaml` and Testcontainers will work.
- Python 3.12 is present — the OKF validator runs today.

**Dependency pins verified:** every version in
[`stack_config.toml`](../../stack_config.toml) was confirmed to exist as a
stable release on nuget.org on 2026-07-29 by querying the flat-container index.
**Co-resolution was not proved** — that needs `dotnet restore --locked-mode`,
which needs the SDK. This is the one pre-flight check that could not be
completed, and it is deliberately the exit criterion of build step 1: if the pin
set does not co-resolve, that is discovered in the first commit rather than the
tenth.

# Deliberate defaults

Decisions taken here that the design doc left open (it closes with ten of them
in its final section). Each is now canonical.

| Decision | Chosen | Why | Revisit when |
|---|---|---|---|
| .NET 10, C# 14 | LTS since 2025-11 | Longest support, current tooling | .NET 11 LTS lands |
| PostgreSQL 17 in Docker | over SQLite | The design doc's resume claim names Postgres; jsonb, precision, and real migrations get exercised | Never for MVP |
| Blazor Server | over React | One language, one toolchain; its SignalR circuit gives live draft re-rank without adding a hub | A public, non-authenticated UI is wanted, or latency to the host becomes a problem |
| 4 projects + 3 test projects | over a simpler split | Explicitly requested as a learning target by the design doc | Never for MVP — this one is intentional over-structure and is allowed to be |
| balldontlie for identity + schedule | free tier | Documented, keyed, stable; teams/players/games is exactly what identity and schedule need | Free tier's 5 req/min becomes limiting, or box scores are needed (paid tier) |
| Basketball-Reference for season stats | scraper | Free, complete, and its season pages are three requests per import | See the robots finding below |
| FantasyPros for ADP | scraper + CSV fallback | ADP must be independent of our own projections or the reach-vs-value feature is a tautology | Page structure churn makes the parser high-maintenance — fall back to CSV |
| Per-minute × minutes projection | over rolling fantasy-point averages | Context deltas need a structural variable to modify; a blended fantasy-point average gives them nothing to attach to | A real ML minutes model replaces the heuristic (post-MVP) |
| Points seed league | design doc's values | Every golden test needs one concrete league | The user's actual league differs — then update the catalog and regenerate goldens |
| No Serilog / Redis / Hangfire / MediatR / AutoMapper | platform features instead | Each solves a problem this app does not have yet; several have licensing traps | A measured need appears, with the measurement |

# Red-team findings from the pre-build cold read

### 1. Rolling windows had no permitted data source *(resolved)*

The design doc requires 7/14/30-day rolling windows, which need per-game stat
lines. Basketball-Reference's `robots.txt` **disallows `*/gamelog/`** — the
obvious source. A requirement with no permitted input.

Resolved: the MVP needs only season-level stats, which come from three permitted
`/leagues/` pages per season. Rolling windows are post-MVP, and when they land
the permitted path is `/boxscores/` (allowed, ~1230 pages per season, so a
rate-limited background worker rather than an interactive import). Recorded in
[scraping_policy](safety/scraping_policy.md) and
[post_mvp_roadmap](tasks/post_mvp_roadmap.md) so the constraint is inherited,
not rediscovered.

### 2. Robots directives verified live, not assumed *(2026-07-29)*

Both scrape targets' `robots.txt` were fetched and read before any code was
specified, rather than assumed permissive. The resulting permitted paths,
disallowed patterns, and crawl delays are recorded once, in
[scraping_policy](safety/scraping_policy.md) — that file is canonical and this
one does not restate its tables.

What matters here is that **these findings expire**. The scraping policy carries
the re-verification procedure and the date each host was last checked; a stale
verification is an unverified one.

### 3. Category leagues were ambiguous in scope *(resolved)*

"Supports category leagues" could mean scoring configuration or the full z-score
and punt analyzer. Split explicitly: the **scoring engine** handles both league
types in the MVP; the **category analyzer** is post-MVP. Without this split, two
competent agents would have built materially different things.

# Knowingly unenforced (prose-only)

Stated out loud rather than assumed. Each is a candidate for promotion to a
real check.

- **"Respect Terms of Use"** beyond robots directives and rate limits. Robots
  paths and crawl rates are machine-checked; ToU compliance is a human judgment
  and stays a human responsibility. **Before running any scraper against a live
  host, the operator should read that host's current terms.** Personal,
  non-commercial use is the assumed posture.
- **Draft weight calibration.** The weights in
  [draft_value_contract](contracts/draft_value_contract.md) are transparent
  constants chosen by judgment, not back-tested. They are configuration, not
  code, precisely so they can be tuned. A wrong weight produces a bad
  recommendation, not a failing test — no check can catch it until there is
  historical validation data.
- **Projection accuracy.** Nothing verifies the projections are *good*, only
  that they are computed as specified and decomposed correctly. Accuracy
  back-testing is post-MVP.

# Build decisions

### Scoring result boundary

The original scoring component signature returned the persisted
`Projections.FantasyValue` from only a `StatLine` and `FantasyLeague`. That
record also requires player identity, projected games, and an optional adjusted
projection identity, none of which the scoring engine owns. The scoring layer
therefore returns the per-game scalar as `decimal`; the projection subsystem
will assemble the persisted record once those inputs exist. This keeps scoring
pure and avoids placeholder identifiers or false season totals.

# Revisit triggers

- The user's real league settings differ from the seed league → update
  [scoring_rules_catalog](contracts/scoring_rules_catalog.md) and regenerate goldens.
- Any scraper parser test fails after a site redesign → fix the parser and bump
  its `ParserVersion`; never loosen the test to make it pass.
- balldontlie rate limits or endpoint coverage change → re-evaluate the
  identity/schedule source before the draft season.
- A build agent had to invent a design decision → that is a bundle gap. Record
  it here and write it into the owning concept.
