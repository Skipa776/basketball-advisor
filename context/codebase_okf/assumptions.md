---
type: assumptions
title: Assumptions, Deliberate Defaults, and Current State
description: What is true right now, what was decided on purpose and why, what is knowingly unenforced, and what should trigger a revisit.
tags: [state, decisions, risks]
source_paths: []
test_paths: []
depends_on: [safety/scraping_policy.md, tasks/post_mvp_roadmap.md]
status: planned
last_updated: 2026-09-20
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

### E05 review — the board has no motion, deliberately

Reviewing E05 against a motion-design rubric surfaced that three of the four
`prefers-reduced-motion` blocks in the UI guarded transitions that animated
nothing, and the one real geometry transition (`transform` on a board row) was
never triggered because Blazor re-renders row *content* while `draft-board.js`
holds the viewport still by correcting `scrollTop`.

That is the right mechanism — it is what D-14 actually asks for, and an animated
row would fight it — so the implementation stayed and the specs were corrected to
describe it. The board and the shell now animate colour only. `D-15` was rewritten
from "a reduced-motion block exists" (which passed while guarding dead CSS) to
"no board row transitions a geometry property and the scroll correction never
animates", which fails if a future change reintroduces movement.

**If positional row animation is ever wanted**, it is a
[design_system_contract](contracts/design_system_contract.md) change first, and it
has to state how the animation and the scroll anchor coexist. Do not add it to the
stylesheet alone.

### E05 review — contrast against the raised surface

`--color-border-strong` was `#606874`, verified at 3.40:1 against `--color-bg` and
never against `--color-surface-raised`, where it measured **2.87:1** — below the
WCAG 1.4.11 floor for an interactive boundary. `.button-link` paints both on the
same element, so the failure was live in the dark theme, not hypothetical. The
token is now `#68707D` (3.84 / 3.56 / 3.23 across the three dark surfaces).

The reason D-11 missed it is instructive: it checked a hand-written list of pairs,
so a surface nobody thought to list was a surface nobody tested. It now takes the
cross product of foregrounds × surfaces and additionally fails if the token file
defines a colour that no pair covers and no exemption names — so adding a token
forces a decision instead of silently widening the blind spot. `--color-border`
was deleted in the same pass: unused, byte-identical to `--color-rule`, and a
third unlabelled name for the value that must never touch an interactive edge.

### First run of the assembled app — two outages the suite could not see

Starting the app by hand for the first time (2026-07-30) found two defects that
192 passing tests had no way to catch. Both are the same shape: **the tests drove
a path no human takes.**

1. **Login was impossible through the UI.** A Razor component route answers every
   HTTP method, so `MapPost("/account/login", …)` on the page's own path made
   routing throw `AmbiguousMatchException` before either candidate ran. Every auth
   test posted JSON to `/api/account/*`, which has no page route and therefore no
   collision, so the whole suite stayed green while the product could not be
   logged into. Form handlers now live at `/account/{login,register,logout}/submit`.

2. **Every static asset was behind the auth fallback policy.** `MapStaticAssets`
   registers endpoints, so `RequireAuthenticatedUser` applied to the stylesheet,
   `theme.js`, `draft-board.js`, and `blazor.web.js`. An anonymous visitor got a
   `302` for each, so the login page — the first screen a new self-hoster ever
   sees — rendered in Times New Roman with blue underlined links and no Blazor.
   D-17 and D-18 parse the returned HTML and never fetch what it links to, so a
   green a11y scan sat on top of a page with no CSS at all.

Row `U-18` now registers and logs in through the **rendered form**, reading the
`action` out of the markup rather than hard-coding it, and asserts the stylesheet
and `blazor.web.js` are reachable anonymously. Both defects were reproduced
against it before the fixes were trusted.

The generalisable lesson, and the one worth carrying into E06–E12: *a test that
authenticates by the convenient path does not cover the path users take, and a
test that parses HTML does not test the page.* When a subsystem gets a
human-facing surface, at least one row must drive that surface the way a person
would.

# Walking the assembled app — the second request was never tested

A manual walkthrough of the running app, after the suite was green at 193 rows,
found four defects. Every one of them lived in the gap between *the request the
test author wrote* and *the second request a user makes*.

- **Every list route answered `400` with no query string.** `GET /api/players`,
  `/api/context-events` and `/api/imports/runs` bound `page` and `limit` as
  non-nullable `int`. A minimal-API value type the caller omits is a binding
  failure, not a zero — so the handler's own `page == 0 ? 1 : page` normalisation
  could never run. `api_surface.md` documents defaults of `page` 1 and `limit`
  50, so the contract was violated on the most obvious call in the API. Every
  functional row passed `?page=1&limit=50`; the rows that used the bare path
  (`RouteIsolationTests`) asserted `401`/`404` and never reached the handler.
  Now `A-35`.
- **My League forgot the league the moment you left the page.** The component
  held the league in a field set only by `CreateLeagueAsync`, and there was no
  `OnInitializedAsync` and no "list my leagues" query anywhere in the stack. A
  user created a league, navigated away, came back, and was told *No league
  configured* while the row sat in the database. Now `A-36`.
- **That break hard-blocked the draft**, which asked for a hand-typed League ID
  GUID with the help text *Use the ID shown on My League* — an ID My League no
  longer showed. The text input is now a `<select>` of the user's leagues, so
  the flow needs no clipboard and no GUID.
- **League validation named the bad token and then threw the name away.** The
  parsers raise `Unknown scoring rule 'PTZ=1'.`; `catch (Exception)` replaced it
  with `The league could not be created.` The one fact the user needed to fix
  their input was the one fact the UI discarded.

The pattern under all four: **a green suite proves the paths someone thought to
write, and the path a user takes is usually the one nobody wrote.** This is the
same failure recorded above for auth, one epic later, in a different subsystem.
The standing counter-measure is the one already stated there — every
human-facing surface needs at least one row that drives it the way a person
would — plus a narrower rule these defects earn:

> A row that supplies every optional parameter has not tested the defaults, and
> a row that reads state in the render that wrote it has not tested persistence.

`ILeagueRepository.ListAsync` relies on the global tenancy filter for scoping
and adds no owner predicate, consistent with every other list query. `U-01`'s
sweep is what keeps that honest.

# Clean shutdown printed a crash — and one thing about it is unexplained

`ImportJobQueue.ExecuteAsync` ran `await foreach (... ReadAllAsync(stoppingToken))`
with the per-job `try` *inside* the loop, so the enumerator's own cancellation
had nothing to catch it. Every clean stop therefore ended with
`BackgroundService failed`, an `OperationCanceledException` stack, and a
critical `StopHost` entry — the log a first-time self-hoster sees on Ctrl-C, and
which they will reasonably read as having broken something.
`RecurringImportWorker` already did this correctly; only the queue did not.
Row `W-04`.

**Known gap, deliberately left:** if `StopAsync` is called immediately after
`StartAsync`, before the queue reaches its first suspension point, `ExecuteTask`
still ends `Canceled` rather than `RanToCompletion`. A marker inside the catch
proved the handler is *not* entered on that path, so the cancellation is not
flowing through `ExecuteAsync`'s body at all, and the cause was not identified.
It does not occur on the real shutdown path — verified by sending the running
app a `SIGINT` and getting zero `BackgroundService failed` and zero `crit:`
lines — so `W-04` drives the queue through real work before stopping it, which
is the ordering a running host actually shuts down from. The startup race is
untested and unexplained. If a future change makes the host log on shutdown
again, start here rather than assuming `W-04` covers it.

# The objective moved to the interface — 2026-07-31

R23 was added and epic E13 scoped, and the OKF now points at them:
`AGENTS.md` carries a current-objective section, the epics README names E13, and
`web_ui_blazor.md` and `test_matrix_ui_design.md` both dropped from
`implemented` to `partial` because rows `D-28`–`D-32` exist and do not pass yet.

**Why a new requirement rather than more R19.** R19 built a vocabulary and a
gate, and both are green. The four defects that shipped anyway — an orphan
`/welcome`, a landing CTA that dead-ends on closed registration, a form asking
for an ID no screen prints, and a second front page for a differently-named
product — are none of them component bugs. Every page is individually correct.
The *path* between them was owned by nothing, so nothing tested it. R23 gives the
path an owner (`web_ui_blazor.md`, which already owns the routes) and five rows.

**A CSS framework was considered and refused, again.** The question was raised
directly — add Bootstrap if that is what a good front end takes. It is not, and
the arithmetic is not close:

- `D-10` scans `.razor` and `.razor.css` for hex colours, raw `px`, and `ms`
  literals. Bootstrap ships thousands. The row fails on import, and there is no
  way to make it pass except by exempting the framework, which is the same thing
  as deleting the row.
- `D-11` computes contrast from the token file. A second palette it cannot see
  makes a green a11y gate a false statement, in both themes and inside `.on-dark`.
- `D-20` requires pages to compose inventory components and define no styles of
  their own. Utility classes in markup are page-level styling with extra steps.
- The token layer already does more than Bootstrap would: two accent tokens held
  to different contrast floors, an always-dark island, a display face barred from
  numeric selectors. A framework would replace a system that knows this product
  with one that knows none of it.

The defects the objective is about — orphan routes, dead-end CTAs, undiscoverable
identifiers, two front doors — are all path defects. **No CSS framework fixes a
path defect.** The refusal stands where it already lived, in
[design_system_contract](contracts/design_system_contract.md).

# Three defects the new path rows found — 2026-07-31

Rows `D-28`–`D-34` were written as tests during E13. Three of them failed on
their first run against code that had a fully green suite, which is the whole
argument for the rows existing.

**Google sign-in led to a 404 (`D-29`).** `GoogleSignInOptions` binds from
configuration, but `AddGoogle()` is never called and no `/account/external/google`
endpoint is mapped anywhere in `src`. An operator who set `Google:ClientId` got a
"Continue with Google" button that went nowhere. `AuthPanel` already carried the
correct rule in a comment — *a provider button that cannot complete a sign-in is
worse than no button* — but the `IsConfigured` gate only checked that a key was
supplied, not that the app could perform the sign-in.

The button is removed. **`GoogleSignInOptions` is deliberately kept**: it is
config plumbing for a feature someone may finish, and deleting it would hide
that the intent existed. Restoring the button requires registering the
authentication handler and mapping the callback first — the flag alone must
never bring it back.

**The sign-in card was clipped on every phone (`D-30`).** `EmptyLayout` declares
`* { box-sizing: border-box }`, but Blazor scoped CSS compiles that to
`*[b-xxxxx]`, so it matched only `EmptyLayout`'s own markup and never reached
`AuthPanel`'s. `.auth-card`'s `width: min(26rem, 100%)` was therefore sizing the
content box, and padding plus border carried it past the viewport.

**This is a trap the whole component library shares.** A reset written in one
scoped stylesheet does not cross into any other component. Anywhere a component
assumes `border-box` because "the layout sets it", it is assuming something that
is not true.

**Context Review asked for an ID no screen printed (`D-32`).** Fixed at the
cause — the review queue prints each event's ID — rather than by adding help
text that explains where to find something invisible.

# What the browser gate was not actually checking — 2026-08-03

Two of its own assertions were dead, and the suite was green over both.

**`D-13` had been clicking the wrong button since the league switcher landed.**
The gate submitted `document.querySelector('form button[type=submit]')`, and the
switcher's form sits in the utility bar, above `<main>` in document order. So
every run posted a cookie change, redirected back to `/draft`, and then timed out
waiting for a pick entry that no draft had started. The failure read
`Browser condition timed out`, which named nothing, so the gate now reports the
page's own state when the combobox never arrives.

**`D-30` walked only the three signed-out routes**, which is the half that was
never at risk — public pages are one column by construction, while the instrument
is built from two-column grids that have to collapse. Given the workspace routes
it failed on the first run, on three separate causes:

- A grid item's automatic minimum is its **min-content** width, so the mobile
  `grid-template-columns: 1fr` overrides refused to shrink below the widest input
  inside them. The desktop tracks already said `minmax(0, 1fr)`; the mobile ones
  did not.
- A `fieldset`'s initial `min-inline-size` is `min-content` for the same reason,
  and needs an explicit `min-width: 0`.
- **A league id is 36 unbreakable characters**, which at the mono size is wider
  than a phone. That one had been shipping since the page existed.

The general lesson is the one worth keeping: **a gate that only walks the front
door tests the surface that was already safe.** The routes behind sign-in are
where the layouts are.

# React and Liquid Glass planning request — 2026-09-18

The owner requested a reviewable migration/design plan before implementation.
The proposal is in [the React migration plan](../../docs/plans/react-liquid-glass-migration.md),
with [researched references and an inspected asset inventory](../../docs/plans/react-liquid-glass-references.md).
It is not approval to replace the stack or promote any concept status.

Planning assumptions, pending the owner's answers: preserve current capabilities,
the Fastbreak name, both themes, the C# engines, cookie authentication, tenancy,
and the draft interaction budget. A single ASP.NET origin with a React build is
recommended to preserve the existing deployment boundary. Visual direction,
public-page rendering, optional UX additions, dependencies, and image use remain
review decisions, not new canonical defaults.

Two source conflicts must be resolved during approved implementation: the latest
UI simplification disables decorative tokens that DESIGN.md still describes,
and E13's blanket likeness ban disagrees with PRODUCT.md/ASSETS.md's later
permission for licensed likenesses. Those latter files still classify the Curry
and LeBron inspiration photos as reference-only. No image rights were inferred,
no assets were copied, and no existing contract was amended by this plan.

### 2026-09-18 — visual review handover

The owner requested completion of the plan and a Playwright UI/UX check before
visual review. Interpreted this as a completed planning package plus a labelled
local HTML concept, not approval to replace the production frontend. The concept
uses fictional data and four representative screens; it does not demonstrate
React runtime performance or production integration. The completed plan records
recommended defaults with owner approval still pending.

Browser review runs against a disposable PostgreSQL database with fictional
records, real Identity flows, background workers disabled, and provider HTTP
blocked. The existing developer database is not used. Photo-bearing captures
remain in the ignored inspiration folder. No concept status or stable contract
is promoted or weakened by these artifacts. See
[the visual review packet](../../docs/plans/visual-review/README.md).

### 2026-09-18 — reference-led redesign after owner rejection

The owner rejected the first visual concept and selected Maxima Therapy and Sam
Walks as the primary foundations. The instruction is to build basketball identity
on those foundations; SimplyRaffle, Webharu and Konny Amaya are backup references.
Created an isolated three-direction study under docs/plans/visual-review with
original scene artwork, local reference photography and an identity-layer toggle.
The daily draft remains a dense, photo-free instrument. This changes the planning
art direction, not production components or canonical token values. Both themes
and all route states remain required for the selected implementation.

The new study is static HTML/CSS/JavaScript with local fictional data, not a React
migration or a production game. Its optional exploration is always bypassable.
No packages, donor-site artwork, canonical scoring values or engine capabilities
were introduced. Owner choice of the final composition and implementation is
still pending. The rejected study remains clearly archived for comparison.

### 2026-09-19 — continuous animated landing study

The owner requested more of Maxima Therapy’s animation and one continuously
scrolling main landing page. Interpreted this as a revision of the authorized
visual review artifact, not approval to replace Blazor or begin the React port.
The selected planning direction is now a five-section vertical landing, with
user-controlled wheel/sign/ball/sticker interactions, scroll-linked decoration,
finite reveals, explicit reduced motion and direct workspace access. Sam Walks
remains a supporting reference; its horizontal introduction is not the main page.

The standalone study lives at docs/plans/visual-review/landing.html. It adds no
packages, production routes, scoring calculations or backend capabilities.
Native scrolling, original vector artwork and locally referenced photos support
review before implementation. Dense draft behavior and canonical statuses remain
unchanged. Offline browser checks and remaining limitations are recorded in the
visual review packet; previous studies are retained for comparison.

### 2026-09-19 — React integration and league-aware intelligence planning

The owner requested an execution plan for React-to-.NET integration, API response
verification, and hot/best-performing player logic under ESPN or Sleeper points
rules, followed by implementation-goal questions. The plan records that the new
frontend is still a static design artifact, while existing C# points/projection/
draft services are reusable. Per-game ingestion and trends remain planned.

Proposed scope sequences the frontend migration and API gaps before selected
E06/R11 and E09/R16 work. Performance, sustainable trend and projected value are
kept distinct; exact league settings remain authoritative, without platform
fallbacks. Platform mode, first workflow, ranking window, freshness and data budget
are unresolved owner decisions. The plan also records the existing box-score
allowlist discrepancy and missing Sleeper bonus/mode representation rather than
silently relaxing a contract. No production code, dependency, safety contract or
concept status changes in this planning pass. See
[the execution proposal](../../docs/plans/react-api-player-intelligence-execution.md).

### 2026-09-19 — owner selected draft-first ESPN and scoring heat

The owner chose draft preparation/live draft decisions, an ESPN-default points
starter because no real league exists yet, and hot as recent fantasy-point
production above a player's expected average. These resolve the previous plan's
primary workflow/platform/meaning questions. The execution plan records published
ESPN weights once, as an explicit editable preparation profile, not a fallback
for missing league rules and not a replacement for the seed fixture. Narrowly
reconcile the catalog's broad provider-default wording during implementation in
accordance with this explicit owner instruction; do not request the same approval
again. No canonical scoring value or implementation changed in this planning edit.

Hot is a descriptive above-expectation comparison, including temporary efficiency
streaks. It must not be silently redefined as the existing opportunity-weighted
TrendScore or automatically added to DraftValue. Expected-benchmark choice,
window and draft setup remain unresolved. Last-five-games versus a disjoint
historical average is the proposed first comparison, not an accepted new contract.
Preseason/old-season samples must be dated and not represented as current heat.
Sleeper-specific work and broader league imports are deferred behind this draft
workflow. The updated execution proposal contains the acceptance sequence.

### 2026-09-19 — expanding/rolling heat baseline and initial team count

The owner specified the first ten season games as the initial player baseline,
then an expanding average through thirty, followed by the latest thirty games.
Interpret games as completed player appearances, not team schedules or DNPs.
This supersedes the earlier uncapped historical-average proposal for descriptive
heat; it does not silently modify baseline projections or the existing stable
sustainable-trend contract. The plan records boundary examples and required tests.
A five-game short comparison window with a reference baseline frozen before that
window remains recommended and has been asked explicitly; its cutoff and window
are not yet accepted. Insufficient history stays visible rather than zero-scored.

The initial league size is seven, with configuration allowing eleven or more
later. The plan requires count-dependent draft behavior and snapshot-preservation
tests rather than hardcoded sizes or silent alteration of an active draft.
Roster slots and draft position remain open; the existing engine's snake ordering
is the proposed first format. This turn updates planning only, with no production
code, canonical status, or safety-policy change.

### 2026-09-19 — three-game hot window selected

The owner answered the explicit short-window question with “Latest 3 games.”
That question described comparison against the baseline before the short window,
so the plan now fixes the three-game comparison and its non-overlapping cutoff.
The first complete comparison is player appearances 11–13 against 1–10; the
reference baseline reaches thirty games when comparing 31–33 against 1–30, then
slides to 2–31 for comparison games 32–34. This resolves the earlier window/cutoff
proposal. The currently defined WindowSpan lacks a three-game member; its exact
representation and tests must be added at implementation without relabelling an
existing span. No engine or canonical status is implemented by this decision log.

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
- **A new page or route ships** → walk it in a browser before calling the epic done.
  Four defects survived a green suite because nobody made the second request.
- **Rows `D-28`–`D-32` go green** → `web_ui_blazor.md` and
  `test_matrix_ui_design.md` return to `implemented`, in that commit.
- **The CSS-framework question is raised a third time** → the answer changes only
  if `D-10` and `D-11` are being retired, not if the front end merely looks
  unfinished. Read the section above before re-arguing it.
- **Someone asks why there is no Google sign-in button** → the handler was never
  written. See above; the config flag alone must not restore it.
- **A component relies on `box-sizing: border-box` it did not declare itself** →
  that reset does not cross a scoped-CSS boundary. Declare it in the component.
- **A comment in `Components/**` mentions a pixel measurement** → row `D-10`
  scans raw file contents, comments included, and will fail on the literal. This
  cost five separate red runs during E13. Write "phone width" or "the page-title
  size", never the number.

## 2026-09-19 — implementation resumed

Owner approved the execution plan and continuation after an account-limit stop.
Patch SSH.NET (already transitive through Testcontainers) to 2026.0.0 as a private
test dependency to resolve NU1903 without suppressing vulnerability checks.
React receives the existing API envelope, numeric enum mappings from a server
catalog, cookie sessions and anti-forgery tokens; no browser-stored credentials.
Seven teams is an editable setup suggestion, not a restriction. No league roster
or draft position is silently assumed: setup asks for these before creation.

## 2026-09-20 — green React integration checkpoint

React `/app` and its .NET bridge passed the full gate: 214 tests, Domain coverage
88.43%, Application 72.92%, five clean Playwright/axe scans, and 73 valid concepts.
Fixtures run in disposable PostgreSQL containers; no developer database or live
provider was touched. Migration remains partial. The owner requested continuation
with the remaining player-intelligence work; the progress ledger names gaps.
The independent ESPN scoring golden is 53, corrected from an erroneous hand total
of 58 caught by the test. No scoring weight changed to accommodate the golden.

## 2026-09-20 — descriptive heat math decisions

Implement heat independently of sustainable opportunity trends. Absolute positive
fantasy-point lift orders the hottest list; current up-to-thirty appearance average
orders best performing. Relative lift divides by absolute baseline value so a
negative baseline retains improvement direction; zero baseline has no percentage.
No arbitrary "very hot" threshold or stale-data cutoff is invented. Results expose
game dates, scored samples, provenance and the effective window policy. The later
API must assess data freshness before calling these retrospective values live.
No scraping policy, source allowlist, stored baseline, or projection math changes.

Heat checkpoint verification: all 20 dedicated cases and the full 234-test gate
passed with Domain coverage 89.19% and Application 72.92%. The pure heat contract
and its own matrix are implemented; opportunity TrendScore, importing, persistence,
and heat API/UI are not promoted. The React/API checkpoint is commit `d74f962`.


## 2026-09-20 — projection publication and draft decisions

Season/source choice is explicit. Manual season rows take precedence over the
chosen automated source for the same player/year; they also include manually
entered players absent from that source. No previous-season fallback is hidden.
Publication timestamps denote calculation start, with ID as a deterministic tie
breaker for equal timestamps. A full-pool run has one publication ID, so switching
pools cannot leave absent players ranked from older runs. Concurrent runs remain
atomic and the newest calculation timestamp wins regardless of completion order.

Values preserve the scoring profile actually passed to the scorer; a concurrent
scoring edit cannot relabel an old result as new. Current reads require profile
equality. Recalculation is explicit after scoring or context edits. The workflow
currently supports points leagues only; category recalculation returns a conflict
without affecting the existing category engine/board. No mathematical projection
constant, draft weight, or heat rule changed.

Legacy projection rows are retained. Unknown observation links and scoring
metadata are not inferred; existing instances must recalculate from their imported
season data before those rows become current rankings. This is preferable to
inventing audit history. The forward migration never rewrites a stored baseline.

React recommendation requests occur on draft-state/recalculation changes and
explicit refresh, not periodic polling, because the existing endpoint saves its
recommendation evidence. Risk evidence is visible outside collapsed details, and
unverified context adds a warning without an extra score penalty. Shortlist height
and loading text reserve space so the player table does not jump after a pick.
Next subsystem: offline per-game storage/parser followed by heat query/UI wiring;
live scraping still requires resolving the documented allowlist mismatch.

Final projection/draft gate passed 243 tests (92 Domain, 35 Application, 116
Integration), zero build warnings/errors, Domain coverage 89.22%, Application
74.33%, five clean axe scans, both draft stability checks and 75 valid concepts.
Review screenshots/report are linked from the progress ledger. Usage checks
returned no quota or remaining-token report. The next handoff remains per-game
storage/parser and heat integration; the overall migration stays partial.

## Completed game storage — 2026-09-21

- This checkpoint implements the offline parser/storage prerequisite authorized
  by the owner, not live fetching. The stable scraping path-table/prose conflict
  stays unresolved; no allowlist or crawl-rate change was made.
- The fixture is deliberately synthetic. A verified saved real page and schedule/
  final-status/identity checks are required before a live importer is enabled.
- Completed box scores are public NBA reference data, like NBA schedule and
  season observations, so the new tables explicitly join the shared-data catalog.
  They carry no league, account or private roster data; existing ownership stays.
- Game phase is explicit, including unknown. Only a caller selecting regular
  season may use those rows for heat; neither parser nor repository guesses phase.
- Corrections are whole-page append-only snapshots. Latest retrieval time wins
  per game/source, with ID only breaking equal-time ties. Phase/date filters run
  afterward to avoid resurrecting stale eligible versions. Sources never blend.
- Snapshot uniqueness includes phase, allowing a later classification correction
  with unchanged HTML. Legacy games receive no manufactured stat rows. An importer
  still owns canonical identity resolution and confirming a game is complete.
- This is a current corrected-data query through a game date, not an as-known-at
  historical replay. Backtesting with an ingestion-time cutoff is a separate scope.

Completed-game checkpoint gate: 271 passing tests (93 Domain, 35 Application,
143 Integration), zero warnings/errors, coverage 89.20% / 74.33%, five clean axe
scans, draft stability checks passed, and 77 valid concepts. Next action is the
owned league heat query and React best/hot views over explicit dated observations.
No live importer or production observations exist from this checkpoint. Quota
checks returned no numerical remaining usage, so no quota estimate is recorded.

## Recorded performance API/UI — 2026-09-21

- An explicit dated historical view meets the owner's heat rule without inventing
  a freshness cutoff. All results disclose unverified completeness/freshness.
  Latest appearance and retrieval timestamps are evidence, not a live status.
- The user chooses a recorded source/season and cutoff. No pool is silently
  selected; empty histories are successful empty responses. No projection is
  converted into a game observation.
- `all` includes players with recorded DNP rows but insufficient appearances;
  observed-player counts are not counts of qualified players. Best and hot use
  the domain calculator's existing ordering. React only formats the response.
- Pagination happens after ranking. Ten rows are shown per browser page; current
  player names are read from the existing shared player API. Scored game evidence
  stays with its league query and private response caching is disabled.
- Current corrected history is not an as-known-at backtest. The active league's
  current scoring is used on every GET; explicit refresh picks up external edits.
- No live fetching, source-policy exception, identity import or production game
  data is introduced. The next game-data prerequisite remains the documented
  stable-policy conflict and verified real saved-page compatibility.

Recorded-performance checkpoint: 277 tests pass (93 Domain, 37 Application,
147 Integration), zero warnings/errors, coverage 89.20% / 75.21%, six clean axe
scans, both draft stability checks and 79 valid concepts. Exact scoring rules now
travel with the computed evidence. The final full-gate log and desktop/mobile
review captures are linked in the implementation progress ledger. Live ingestion
remains blocked on explicit source-policy resolution; no production game rows
were created and no numerical usage quota was available.

## React functional pages — 2026-09-22

- Category league setup can persist explicit categories, but category valuation,
  draft rankings and performance analysis are not implemented. Until those
  capabilities exist, the React workspace hides points-based advice for category
  leagues and explains the limitation. This keeps a category selection from
  implying points values are category-aware.
- Saved-draft lists are scoped to a selected league because ownership is enforced
  at league boundaries. The interface uses opaque league/draft ids in URLs and
  displays pick counts without claiming creation order; draft rows have no
  creation timestamp.
- Roster-slot order is display order, while structural draft compatibility depends
  on team count and counts of each slot kind. Reordering the same slots must not
  block a league name or cadence edit after a draft exists.
- Trade, streaming and standings remain clear not-built destinations so signed-in
  navigation can expose the intended product path without fabricating engine
  output.
- The full loopback/Testcontainers gate passed on 2026-09-22 after the test
  process was granted the local socket it needs: 279 tests, browser navigation,
  accessibility and mobile checks, and coverage floors. React migration remains
  partial because live imports and the remaining Blazor routes are not complete.

## Box-score import — 2026-09-23

- balldontlie `/v1/stats` returns 401 on the free tier, so per-game lines come
  from Basketball-Reference game pages, owner-authorized 2026-09-23.
- The importer runs as an `ImportJobKind` on the existing owner job queue rather
  than a new hosted worker: the queue already serializes imports, records runs
  and shares the host rate limiter. A recurring box-score worker is not built.
- Game dates use US Eastern time because the NBA schedules and Basketball-
  Reference game ids use the local Eastern date; balldontlie stores UTC tip-off.
- Owner test window for the landing/heat trial: random season day 2025-11-16,
  following month 2025-11-16 → 2025-12-15. 2025-12-16 (NBA Cup final, not a
  regular-season stat game) is excluded.

## Public landing data — 2026-09-23

- Featured players are an owner-approved constant list of 30 consensus top
  fantasy players for 2025-26 (`Application/Landing/FeaturedPlayers.cs`), matched
  by normalized name. A name with no stored appearance that day is simply absent.
- "CAT" is categories won out of the nine standard categories versus the mean of
  every player who played that date: counting stats beat the mean, FG%/FT% beat
  the pool's combined makes/attempts (zero attempts never wins), fewer turnovers
  win, ties win nothing. It is a descriptive single-day figure, not a category
  valuation engine.
- Points use ESPN default scoring, now owned by `LeagueCatalog.EspnDefaultPointsRules`
  (the setup catalog reads it; previously it was typed inline there).
- Risers exclude featured players (the "usually on the waiver" proxy — there is no
  roster-ownership source). Status thresholds live in `RiserStatus`: Must add at
  ≥ +40% with a 3+ game streak, Add at ≥ +20%, Watch above 0%, otherwise Hold.
  Streak counts consecutive latest appearances above the player's baseline mean.
- The endpoints are anonymous because the data is public reference data; they are
  rate-limited per IP and never read owned tables.
- Team codes are omitted from the landing payload: players carry a team id but no
  by-id team lookup exists; add one when the cards need it.

## Full-season replay — 2026-09-23

- The whole 2025-26 regular season (2025-10-21 → 2026-04-12) is imported. Play-in
  games (Apr 14–17) are flagged `postseason: false` by balldontlie, so they are
  excluded by date, as is the Dec 16 NBA Cup final.
- `Landing:AsOf` replays the season: set it to a date such as 2026-03-01 and the
  landing and waiver views use only games before that day. With a full season the
  accepted 10-game baseline applies; no heat override is needed.
- The featured strip shows every featured player's most recent game rather than
  only those who played the resolved day, so it is full on light schedules.

## Rosters, waiver and matchup — 2026-09-23

- Owner: rosters from the platforms. With no Sleeper league to validate against,
  only the CSV rung ships; ESPN stays CSV/manual per R16.
- Waiver availability = not on any imported roster. Without rosters the featured
  stars are excluded instead, and the page says so.
- Matchup (owner: weekly points total). Weeks run Monday–Sunday on US-Eastern
  dates, as ESPN and Yahoo weekly matchups do. Per-game projection is the average
  over the last 10 appearances under the league's scoring (recent form, available
  all season); a player without appearances contributes nothing and is labelled.
  Games left come from the stored balldontlie schedule for the player's current
  team (weekly directory refresh). Lineup limits and daily start/sit are not
  modelled: every rostered player's games count.
- Category support is deferred by the owner; waiver and matchup return 409 for
  category leagues.

## Draft market sign — 2026-09-23

Owner-approved: `MarketValue = (currentPick − ADP) × valuePerPick`, matching the
draft value contract's own definition. See that contract's correction note.

## Streaming prerequisites — 2026-09-23

- Eligibility (owner): Basketball-Reference primary position (PG/SG/SF/PF/C)
  through the existing `RosterSlot.Accepts` mapping; no multi-position eligibility.
- Weekly acquisitions (owner): per league, default 7 (ESPN's common default),
  1–99, editable on create and in League settings.

## Sleeper import — 2026-09-24

- Owner: Sleeper only for now. Rosters and per-league eligibility are imported;
  settings are compared and reported, never applied (no diff-and-confirm flow yet).
- The user picks which Sleeper roster is theirs; the roster with an owner is
  preselected. Unclaimed rosters are named "Team {roster id}".
- Sleeper's per-player `injury_status` is available in the player map and is not
  used yet; it is a candidate availability feed.
