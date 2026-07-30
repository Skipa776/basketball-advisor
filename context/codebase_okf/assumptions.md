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

**Build step 4 is implemented.** EF Core has generated forward migration and
snapshot files, explicit per-entity configuration, snake-case PostgreSQL
storage, JSONB stat lines, string enums, numeric precision, uniqueness indexes,
append-only baseline enforcement, and repositories that round-trip leagues,
players, season stat lines, draft state, recommendations, and evidence. Tests
use isolated Testcontainers PostgreSQL 17
instances and cover migration from empty, duplicate provider identity,
baseline immutability, enum storage, dependency direction, repository shape,
league/draft cascade versus player-history restriction, pending identity
matches, immutable import runs, idempotent draft picks, and recommendation
round trips.

**Build step 5 is implemented.** Exact player-name normalization, the six-tier
identity ladder, canonical source names and confidence defaults, provenance
validation, ambiguity review records, and atomic player-import accounting are
implemented. Rows N-01 through N-05 pass, including the real EF transaction and
queryable import-run path.

**Build step 6 is implemented.** Named factory clients share response caching,
the platform resilience stack, and a singleton per-host token-bucket limiter.
Failed and canceled imports roll back on a real PostgreSQL transaction; failures
are recorded without escaping the use case. Failed or stale automated sources
lower recommendation confidence and add explicit data-quality risk evidence.

**Build step 7 is implemented.** The balldontlie adapter maps teams, cursor-
paginated players, and games from recorded JSON, resolves canonical teams,
converts timestamps to UTC, hashes raw fragments, and persists teams and games
with provenance. Its named client reads the validated API key from options and
shares the safe HTTP pipeline. No real key or network call is used in tests.

**Build step 8 is implemented.** The Basketball-Reference adapter fetches only
the three allowlisted season pages, parses comment-wrapped tables from recorded
HTML, maps the canonical counting-stat vocabulary, resolves player identity,
converts usage percentage points at the boundary, rejects inconsistent rebound
rows and schema drift, and stamps stable scraper provenance. Its URL builder
rejects gamelog paths, foreign hosts, query strings, fragments, and every path
outside the stable scraping policy.

**Build step 9 is implemented.** FantasyPros HTML, CSV, and manual ADP providers
produce one validated provider record with source-appropriate provenance.
FantasyPros uses an exact-path URL gate and a pure fixture-backed parser; CSV
and manual work with the scraper absent. `ImportAdpService` resolves player
identity and writes one canonical, provenance-complete ADP entity through a
forward EF migration and append-only repository surface.

**E01's implementation slices, build steps 5–9, are complete.** Its live-import
exit check is not certified in this repository session because no operator API
key was used and no scrape target was contacted; W-01 through W-03 belong to
the later background-worker subsystem and also remain pending.

The full gate passes 70 Domain, 30 Application, and 47 integration tests. Domain
line coverage is 88.42%, Application line coverage is 73.01%, the build has zero
warnings, and all 72 OKF concepts validate.

**Build step 10 is implemented.** League-average rate shrinkage, minutes and
durability projection, ratio recomputation, zero-history behavior, and the
worked scoring example are covered by pure Domain tests. Observed statistics
and statistical baselines persist as separate append-only rows, with rounding
only at the repository boundary.

**Build step 11 is implemented.** The live board recomputes replacement level,
scarcity, roster redundancy, market value, and risk from current session state;
manual picks and undo restore availability, category leagues show an explicit
fallback, and every ranked recommendation carries structured evidence. A full
10-team, 13-round manual mock draft completes within the tested rerank latency.

**Build step 12 and epic E02 are implemented.** Context events use the complete
catalog, proposed/verified/rejected human review, audited impact overrides, and
computation-time expiry. Pure context application creates a separate adjusted
projection without mutating its baseline, and the four projection records now
round-trip through PostgreSQL.

**Build step 13's HTTP and persistence slice is implemented.** The envelope,
validation, sanitized exception mapping, structured request logging, and all
MVP league, player, import, draft, context, recommendation, and source-health
routes run through the real Kestrel host and an isolated PostgreSQL database.

**Build step 14 is implemented.** One Blazor Server host provides Dashboard, My
League, Players, Draft Assistant, Context Review, and Data Sources. The UI calls
Application services in process; projections remain decomposed and visibly
unverified where applicable; recommendations never appear without evidence;
pick entry supports type, arrows, Enter, and focus restoration.

**Build steps 15 and epic E03 are implemented.** Schedule, stats, and ADP each
have their own hosted service, configurable cadence, staggered startup, and the
shared import queue's fresh scope per run. Worker failure, continuation,
shutdown, registration, and configuration rows pass. All ten MVP stories now
have implementation and automated evidence; live external-source certification
still requires operator credentials and remains explicitly under-claimed.

**Build step 16 and epic E04 are implemented.** Identity and ownership share the
existing context and migration history. First registration atomically claims
pre-auth rows before the second migration makes required ownership non-null.
Marker-driven filters and explicit endpoint checks isolate every owned route;
the reflection sweep covers 10 routes and its deliberately leaky fixture proves
it can fail. Secure cookies, closed registration, lockout, anti-forgery, HTTPS,
worker isolation, cascade deletion, and portable JSON export/import pass U-01
through U-17.

**Build prerequisites on the development machine:**

- **.NET 10 SDK is installed.** SDK 10.0.302 satisfies `global.json` through
  `rollForward: latestFeature`. Verified 2026-07-29.
- Docker 28.5.1 is present — `compose.yaml` and Testcontainers will work.
- Python 3.14.6 is present — the OKF validator runs today.

**Dependency pins verified:** every version in
[`stack_config.toml`](../../stack_config.toml) was confirmed to exist as a
stable release on nuget.org on 2026-07-29 by querying the flat-container index.
`dotnet restore --locked-mode` now proves the committed lock files co-resolve
under SDK 10.0.302 on every full gate run.

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

## Identity-ingestion shapes not specified upstream

The contracts name `PendingIdentityMatch` and `DataImportRun` but do not define
their property shapes. The implementation records a pending match's provider,
external id, raw and normalized names, candidate player ids, UTC creation time,
and reason. An import run records source, `Running | Succeeded | Failed`, UTC
start/finish, rows written, pending count, and failure detail. Both use
application-generated GUIDs. `DataImportRun` is stored append-only; the
synchronous import use case writes its terminal snapshot once. The later API
slice must preserve append-only storage when it exposes the contract's immediate
`Running` response, rather than adding an update method.

The API queue therefore holds only active `Running` snapshots in process and
persists one terminal snapshot with the same identifier when work completes.
A host restart can forget an in-flight display row, but it cannot rewrite or
misreport persisted history. This is the smallest design that preserves the
append-only contract; durable job recovery remains post-MVP.

## API operational defaults not specified upstream

Source health becomes stale after two days unless configuration overrides the
duration. This is deliberately neutral across the three automated sources; the
recurring-worker slice may configure a shorter cadence without changing the
health contract. A source is degraded when it is stale or its latest failure is
newer than its latest success.

Import failure detail stores the exception type, not the exception message.
Messages from remote clients and database providers can contain URLs, keys, or
connection details; the type is sufficient for the operator-facing status
surface while structured server logs retain the trace identifier. The public
error envelope likewise returns a stable generic message for unexpected
failures.

The recurring refresh defaults are one day per source, staggered one, five, and
ten minutes after startup for schedule, stats, and ADP respectively. Schedule
refresh covers yesterday through fourteen days ahead so a corrected recent game
or near-term schedule change is included without widening the request window
indefinitely. The NBA season end year rolls forward in July. ADP runs only from
July through October by default; those active months, every cadence, startup
delay, and schedule window are configuration values.

Login and registration use a fixed-window limit of 10 attempts per IP per minute.
The auth contract requires per-IP limiting but does not assign a numeric budget;
this permits ordinary setup retries while bounding credential and registration
bursts. Revisit if deployment telemetry shows legitimate setup flows exhausting
the window.

Account archives contain every table mapped by `IOwnedResource`, discovered from
the EF model and ordered by its foreign-key graph. PostgreSQL `jsonb` composite
record import keeps the format schema-complete without a second hand-maintained
entity list. Primary and owned foreign keys are remapped on import so an archive
can be restored into another empty account on the same instance; shared player
and baseline identifiers remain references to the one global data set.

## ADP shapes and fallback inputs not specified upstream

The provider contract named `AdpEntry` and the persistence contract listed an
entity with the same name, but neither defined its properties. The provider
boundary now carries external id, raw player name, positive average draft
position, optional non-negative standard deviation, and provenance. The
canonical Domain entity replaces raw identity with `PlayerId`, adds its
application-generated GUID, and retains the value, optional variance, and
provenance. `ImportAdpService` is the only mapping point between them.

The CSV rung uses the fixed header
`external_id,player_name,adp,standard_deviation`; the last field may be blank.
Manual input uses the same four values. This keeps all rungs isomorphic and
avoids provider-specific branches downstream. The full CsvHelper dependency
remains reserved for the later league-import epic, where multiple schemas and
template round-tripping justify it.

## Retry attempts are individually rate-limited

The ingestion diagram originally placed the rate-limit handler outside
resilience. In an `HttpMessageHandler` chain that throttles only the logical
request; retry attempts invoke the inner handler directly and can exceed the
non-negotiable six-per-minute ceiling. The implemented and now-documented order
keeps the cache outermost but places resilience outside the per-host limiter, so
every actual send—including every retry—must acquire a permit. This strengthens
the scraping safety boundary; it does not change the retry budget or allowlist.

## Canonical schedule and team-source persistence shapes

The provider contract names `ExternalTeam` and `NbaGame` without defining their
properties. `ExternalTeam` carries the source id, raw name and abbreviation,
and provenance. `NbaGame` has an application-generated GUID, season end year,
UTC start, canonical home/away team ids, nullable scores, source status, and
provenance; the provider id lives only in provenance. Teams are resolved by
their unique NBA abbreviation. A separate `nba_team_source` audit row stores
the team import's source id and full provenance without adding source fields to
the canonical `NbaTeam` shape or backfilling invented provenance onto existing
teams.

## Context certainty where upstream factors are unavailable

The catalog requires proposed events to apply at reduced confidence but does not
assign the reduction. Proposed event certainty is therefore half of the event's
confidence value; the full impact still applies and
`HasUnverifiedContext` remains visible. `ContextApplier` cannot derive sample
size, import freshness, or source quality from `BaselineProjection`, so those
three confidence factors use a conservative neutral `0.5` until the later
source-health/application orchestration slice supplies measured values. Event
`ProjectionConfidenceDelta` overrides adjust the context-certainty factor after
that reduction and the result is clamped to `[0,1]`.

For default impacts, catalog cells marked `±` follow the event direction; cells
with a fixed sign (`Injury`, `BenchRoleChange`, `MinutesRestriction`,
`RestRisk`, and return-from-injury minutes) retain that sign. Role-risk deltas
are always non-negative uncertainty additions. This preserves the catalog's
fixed-sign rows instead of multiplying a recorded negative twice.

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

# Post-MVP scoping decisions — 2026-07-29

The project was scoped from MVP to a publishable, self-hostable product. R11–R22 in
[`PROJECT_REQUIREMENTS.md`](../../PROJECT_REQUIREMENTS.md); build order and prompts in
`docs/epics/`.

| Decision | Chosen | Why | Revisit when |
|---|---|---|---|
| Shipping shape | Self-hostable product | A container image and a one-command quickstart; no hosted service to operate | Someone wants a hosted instance |
| Auth | **Full ASP.NET Identity** with real per-user ownership | Chosen deliberately over a single-user password. It only earns its place if ownership is real, so the data model is genuinely multi-tenant and reference data is shared rather than duplicated | Never — half-tenancy would be worse than either end |
| Auth ordering | Epic **E04**, fourth | The isolation sweep enumerates routes by reflection, so every later endpoint is covered automatically. Landing auth last means retrofitting a dozen endpoints at once | Never |
| Front-end | Blazor Server, invest in the design system | No second toolchain; the SignalR circuit already gives live draft re-rank | A public non-authenticated UI is wanted |
| Design process | Structure specified, taste elicited | Tokens, inventory, and the a11y floor need no human; the aesthetic does. `DESIGN_BRIEF.md` → `/design-consultation` → `DESIGN.md` | Never |
| LLM scope | News → **`Proposed`** context events only | The human-in-the-loop seam already existed. Statistical code still makes every recommendation | Never — see [llm_trust_boundary](safety/llm_trust_boundary.md) |
| LLM default | **Disabled**, no key required | A self-hoster without a key must get a complete application | Never |
| LLM provider | Official `Anthropic` SDK, `claude-opus-5` | Structured outputs constrain the response to the event schema; effort is the cost lever. The community `anthropic.sdk` package is forbidden | Model or SDK deprecation |
| Statistics dependencies | None — implement `Φ` and rank correlation | Two functions of a few lines each, one test each. `MathNet.Numerics` is forbidden | A third non-trivial statistic appears |
| Automated transactions | **Permanently out of scope** | Yahoo integration requests read-only scope. A tool that recommends and a tool that acts have different failure modes, and the second one loses someone's season to a bug | Never |
| Trade fairness for the other side | **Not offered** | No model of their needs exists; a verdict without one is authoritative-looking noise | Never |

# Red-team findings from the post-MVP cold read

Four more spec defects caught before any of this code existed.

### 4. The streaming score double-counted usable games *(resolved)*

The design doc's formula multiplies `ExpectedPerGameValue × UsableGames × …`. Summing
per-day value and *then* multiplying by the count of usable days counts every day
twice. Resolved in [streaming_contract](contracts/streaming_contract.md): the value is
a **sum across usable days**, and `UsableGames` is the cardinality of that sum —
reported to the user, never a multiplier on it.

### 5. Category z-scores on raw percentages are wrong *(resolved)*

The obvious implementation z-scores `FG_PCT` and `FT_PCT` directly, which makes a
player who took two free throws and made both a maximal free-throw asset. Resolved in
[category_value_contract](contracts/category_value_contract.md) with the volume-weighted
*impact* form, and pinned by a required test (row Y-01) that asserts exactly that
player scores near zero.

### 6. The punt ceiling is arithmetic, not taste *(resolved)*

"How many categories can you punt" has a derivable answer: winning 9 categories
requires 5, so punting `k` leaves `9 − k` from which 5 must come — feasible only while
`k ≤ 4`. `PUNT_MAX = 3` leaves one category of margin. Recorded with the derivation so
nobody re-argues it from intuition.

### 7. Survival probability would have double-counted `MarketValue` *(resolved)*

Adding a survival term alongside the MVP's `(ADP − pick)` proxy would have counted
"the market says wait" twice. Resolved in
[draft_intelligence_contract](contracts/draft_intelligence_contract.md): `OpportunityCost`
**replaces** `MarketValue`, and E07 edits
[draft_value_contract](contracts/draft_value_contract.md) in the same commit. A rename
that leaves the old term callable is not a replacement.

# Knowingly unenforced (prose-only)

Stated out loud rather than assumed. Each is a candidate for promotion to a
real check.

- **"Respect Terms of Use"** beyond robots directives and rate limits. Robots
  paths and crawl rates are machine-checked; ToU compliance is a human judgment
  and stays a human responsibility. **Before running any scraper against a live
  host, the operator should read that host's current terms.** Personal,
  non-commercial use is the assumed posture. Now also a line item in
  [release_checklist](tasks/release_checklist.md), so someone owns it per release.
- **Draft weight calibration.** The weights in
  [draft_value_contract](contracts/draft_value_contract.md) are transparent
  constants chosen by judgment, not back-tested. They are configuration, not
  code, precisely so they can be tuned. A wrong weight produces a bad
  recommendation, not a failing test — no check can catch it until there is
  historical validation data. **Scoped for resolution: epic E11**
  ([backtest_contract](contracts/backtest_contract.md)).
- **Projection accuracy.** Nothing verifies the projections are *good*, only
  that they are computed as specified and decomposed correctly. **Scoped for
  resolution: epic E11.** Note the honesty rule there — if the fitted weights do
  not beat these judgment defaults on holdout, the defaults ship and this entry
  stays, citing the report that says so.
- **Trend weights** in [rolling_window_contract](contracts/rolling_window_contract.md)
  are judgment values on the same footing, and subject to the same rule.
- **No fuzzy player matching**, by choice
  ([player_identity_contract](contracts/player_identity_contract.md)). There is no
  fuzzy matcher to test; the invariant is the absence of one.
- **Prompt wording** is not a security control
  ([llm_trust_boundary](safety/llm_trust_boundary.md)). The tested controls are the
  absent tools, the constrained schema, the grounding check, and the unreachable
  `Verified` state — the prompt is the part an attacker gets to argue with.

# Conflict with the global C# rules

`~/.claude/rules/csharp/testing.md` recommends **FluentAssertions** and **Moq**. Both
are on this project's forbidden list in
[`stack_config.toml`](../../stack_config.toml): FluentAssertions moved to a commercial
licence at v8, and Moq shipped a telemetry component. The rules file predates both
changes. This project uses **Shouldly** and hand-written fakes; NSubstitute is
available with approval. Where the global rules and `stack_config.toml` disagree,
`stack_config.toml` wins — it is the machine-checked one.

# Build decisions

### Scoring result boundary

The original scoring component signature returned the persisted
`Projections.FantasyValue` from only a `StatLine` and `FantasyLeague`. That
record also requires player identity, projected games, and an optional adjusted
projection identity, none of which the scoring engine owns. The scoring layer
therefore returns the per-game scalar as `decimal`; the projection subsystem
will assemble the persisted record once those inputs exist. This keeps scoring
pure and avoids placeholder identifiers or false season totals.

### E05 back-test chart inventory

E05 explicitly requires a back-test calibration chart, while the design-system
component inventory originally named no component that could own it. The inventory
now includes `BacktestCalibration` before the component implementation. It remains
presentational and accepts calibration points; producing those points stays in E11,
so E05 does not fabricate historical results or pull post-MVP computation forward.

# Revisit triggers

- The user's real league settings differ from the seed league → update
  [scoring_rules_catalog](contracts/scoring_rules_catalog.md) and regenerate goldens.
- Any scraper parser test fails after a site redesign → fix the parser and bump
  its `ParserVersion`; never loosen the test to make it pass.
- balldontlie rate limits or endpoint coverage change → re-evaluate the
  identity/schedule source before the draft season.
- A build agent had to invent a design decision → that is a bundle gap. Record
  it here and write it into the owning concept.
- **A back-test run completes** → update the two calibration entries above with the
  measured result, whichever way it came out.
- **Anthropic deprecates the model or changes the SDK surface** → the `[llm]` model is
  a config value, but a parameter-shape change needs the `claude-api` skill re-read
  before editing the adapter.
- **A new prompt-injection shape is thought of** → add an adversarial fixture to
  `test_matrix_llm.md` row M-02. Adding one needs no approval.
- **An owned entity is added without `IOwnedResource`** → the U-01 sweep should catch
  it. If it did not, the sweep is the bug, not the entity.
