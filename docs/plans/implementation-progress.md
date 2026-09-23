# React and player intelligence implementation progress

Updated: 2026-09-22. Accepted decisions and full scope are in
[the execution plan](react-api-player-intelligence-execution.md).

## Current checkpoint

This is a working React/API vertical slice, **not the completed migration or
player-intelligence implementation**. Existing visual studies and Blazor routes
are preserved.

| Milestone | Status | Delivered / remaining |
|---|---|---|
| M0 — baseline | Complete | SSH.NET security repair; approved scope/toolchain recorded; baseline gate green |
| M1 — React foundation | Partial | Pinned React/TypeScript/Vite project, .NET-served `/app`, typed API boundary, loading/error states. Continuous animated landing and complete design-system port remain |
| M2 — basic features | Partial | Account/session, editable ESPN setup, league selection/settings, player search/detail, snake draft, saved-draft list, recommendations, pick/undo and URL reload are connected. Live imported data and full category analysis remain |
| M3 — verification | Partial | Real HTTP and PostgreSQL tests; cross-user isolation; browser journey, axe scans, desktop/mobile screenshots. Full parity matrix and owner visual acceptance remain |
| M4 — game data | Partial | ESPN setup catalog and independent scoring golden delivered. Offline box-score parser and atomic per-game snapshots delivered; identity/import worker, live compatibility, progress and data sufficiency remain |
| M5 — heat | Partial | Pure C# calculation and best/hot ranking logic pass 20 offline cases; stored data, authenticated dated query and React views delivered; live ingestion/freshness still unverified |
| M6 — player intelligence UI | Partial | Draft-value shortlist/evidence/decomposition delivered; separate observed best-performing/above-baseline/all views with game evidence delivered; live-data readiness and full draft-detail parity remain |
| M7 — release | Pending | Remaining Blazor migration, distribution, live-data regression and owner review |

## What is implemented

- `/app` is a real React client served by the same .NET origin. Dashboard links
  into it; the existing app remains reachable. Build frontend before .NET so the
  static asset manifest includes its generated files.
- JSON APIs added: `GET /api/account/session`, `GET /api/leagues`,
  `GET /api/leagues/setup`, and `GET /api/drafts/{id}`. Existing mutation APIs and
  envelope remain authoritative. Session responses are non-cacheable.
- Secure HttpOnly cookies and fresh anti-forgery tokens drive mutations. No
  credentials or owned account data are stored in browser local storage.
- The ESPN profile has one executable owner, `LeagueSetupCatalog`. It is an
  explicit editable setup choice. Empty scoring remains invalid; seed goldens
  remain unchanged. Numeric enum values outside the defined catalog are rejected.
- Seven teams is the editable starter; the HTTP tests also exercise eleven.
  Roster slots, draft position and rounds require user input. Drafts remain snake
  drafts using the existing server engine.
- Picks and undo survive reload because the URL identifies a persisted session.
  Duplicate/conflict behavior remains server-owned. A completed draft now rejects
  extra picks in the domain, and undo reopens the final slot.
- Projections display observed sample, baseline, adjusted values and final league
  points separately, with a visible unverified-context warning. Missing imports
  and missing projections are explicit; the production UI contains no fixtures.

## Verification ledger

Toolchain: .NET SDK 10.0.302, Node 24.13.0, npm 11.6.2. Exact frontend/test pins
are in `stack_config.toml` and the npm lockfile.

- Baseline `bash scripts/gate.sh`: passed 209 tests (70 Domain, 30 Application,
  109 Integration), D-14 stability at 0.000px, Domain coverage 88.42%, Application
  72.92%, and 72 valid OKF concepts.
- `dotnet restore --force-evaluate`: succeeded after patching the existing
  Testcontainers SSH.NET dependency. Vulnerability checks were not suppressed.
- `npm ci --ignore-scripts`: succeeded; npm reported zero vulnerabilities.
- `npm run build`: strict TypeScript check and Vite production build succeeded.
  Vite leaves the existing `/fonts/anton-latin.woff2` URL for the .NET host to
  resolve; this is expected, and browser checks load the actual host assets.
- New HTTP checks passed for session identity, closed registration, explicit
  ESPN scoring (independent 53-point golden), persisted eleven-team draft reload,
  paging and cross-user access. Undefined enum regression is in the final gate.
- Browser checks found and fixed 320px overflow caused by offscreen accessible
  text placement. All five axe scans then passed. The flow also exposed an
  ambiguous league-selector label, which was corrected.
- Navigation checks now discover host GET routes alongside Razor `@page`
  directives; `/app` is tested rather than exempted from route checks.
- Final `bash scripts/gate.sh`: **passed**, 214 tests (71 Domain, 30 Application,
  113 Integration), zero build warnings, D-14 stability 0.000px, Domain coverage
  88.43%, Application 72.92%, and 73 valid OKF concepts. Playwright completed
  registration, setup, keyboard pick, reload, undo, logout and login; all five
  axe scans had zero violations and neither mobile width overflowed.

Browser screenshots/report (generated under ignored test output):

- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/desktop.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/mobile-390.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/mobile-320.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/report.json`

## Functional app pages checkpoint — 2026-09-22

Added React pages for owner data imports and freshness, saved drafts, league
settings, context review, and account archive/delete actions. The `/app/{page}`
host route serves the same client; all destinations are in signed-in navigation.
Trade, streaming, and standings pages state their unimplemented requirement and
show no invented scores. Category creation is supported; category draft analysis
is clearly unavailable.

Added owner-scoped, paged `GET /api/drafts?leagueId=` and
`PUT /api/leagues/{id}/settings`. The latter permits name/cadence changes while
drafts exist and refuses team-count or roster-slot changes after the first
session. Stable secrets policy still requires the BallDontLie key at startup, so
the planned in-app missing-key state cannot be reached without changing that
policy. The owner page shows source freshness and import runs after startup.
Import controls were not activated and no external provider was contacted.

## Live blank-page repair — 2026-09-22

The running local host served the new `/app` index after a frontend rebuild,
but its startup static-asset manifest did not include the newly fingerprinted
JavaScript and CSS. Anonymous asset requests were redirected or received HTML,
so the browser could not mount React. The host now serves physical public files
before endpoint routing. An offline HTTP regression creates a new JavaScript
file after host startup and checks for `200` with a JavaScript content type.
The full gate passed 280 tests with zero build warnings; the live app rendered
after another frontend rebuild with no browser errors.

Verification: `bash scripts/gate.sh` passed on 2026-09-22 after allowing its
loopback test processes. React and .NET built with zero warnings; 93 Domain,
37 Application, and 149 Integration tests passed. The browser journey opened
all new destinations, scanned accessibility, checked phone overflow, and verified
draft-row stability. Coverage floors passed (Domain 89.20%, Application 73.51%);
80 OKF concepts validated, including the planned seasonal player evidence
contract. No provider was contacted by tests.

One immediately preceding full-gate attempt timed out in the older Blazor D-14
post-pick offset check after the pick had committed. Its isolated rerun and this
full rerun both passed with a 0.000 CSS-pixel offset delta. This is an
intermittent browser-gate risk to watch during owner testing.

The browser runs inside the real-cookie integration fixture with its own
throwaway PostgreSQL database and blocks external requests. No developer data
or live provider was used. Screenshots contain clearly named fictional test data.

## Usage and handoff

Budget checks were made at startup, after the API slice, during browser work and
before wrap-up. Every `get_goal` result returned no active goal, remaining-token
value or completion budget report. Account usage quota is not exposed; no numeric
remaining-usage claim is possible. The owner requested continuation after the green React checkpoint. Continue at tested subsystem boundaries and do not leave partially tested work
when the context budget approaches its limit.

The earlier automatic-review account-limit block was cleared by the owner's
continuation; restore and subsequent approved tooling commands ran successfully.

## Single next action

Resolve the **game-data source-policy prerequisite**, verify the parser against a
real saved page, then implement canonical identity/schedule/final-status checks
and the resumable importer with visible progress. The stable scraping allowlist
still excludes box scores despite contradictory prose; no live fetching is enabled
until that conflict is explicitly resolved. The query/UI can be reviewed using
synthetic offline test data, but production truthfully shows no observations.

Preserve the accepted heat rule unchanged: first complete comparison after
appearance 13; latest three excluded from the comparison baseline; baseline grows
from ten to thirty and then rolls at thirty; DNP is not zero; seasons stay separate.
No current/live hot badge or arbitrary freshness cutoff was added. League
settings, saved-draft listing, and the visual design now have tested React pages;
live game ingestion and the remaining Blazor migration are separate work.

## Heat calculation checkpoint — 2026-09-20

`PlayerHeatCalculator` now computes league-specific per-game fantasy scores,
current and comparison averages, point/relative lift, counts above baseline, and
separate best-performing/hottest rankings. Effective policy, model version, game
dates, IDs and provenance remain attached. No current-season fallback and no
invented sustainability or freshness claim. `PlayerGameSample` distinguishes
DNP/incomplete appearances and requires statistics/provenance where appropriate.

`dotnet test ... --filter PlayerHeat` passed **20 cases**. The full gate also
passed: **234 tests** (91 Domain, 30 Application, 113 Integration), zero build
warnings, Domain coverage 89.19%, Application coverage 72.92%, five clean axe
scans, D-14 stability 0.000px, and 75 valid OKF concepts. This completes the pure math module only; M4/M5 are still partial.
Usage check after unit tests again returned no budget/quota value.

### Next-step code findings

- `ProjectionService.ProjectPoolAsync` is registered but has no production
  caller. New leagues can legitimately have no `FantasyValue` rows even when
  players have been imported. Wire projection generation/revaluation explicitly
  and test it from imported season stats through the actual ranking response.
- `DraftCandidateRepository` and `ProjectionRepository.GetFantasyValueAsync`
  select the latest value by descending random GUID. GUID ordering does not
  establish recency. Correct version/recency selection, scoring-change behavior,
  and projection decomposition consistency before treating rankings as current.
- `GetLatestDecompositionAsync` starts from the latest shared baseline rather
  than a coherent league value/adjusted/baseline chain. Add regression cases with
  multiple baselines and multiple leagues so another projection run cannot make
  a valid league decomposition disappear or mix versions.

These findings are documented for the next integration step; this checkpoint
has not silently changed persistence or projection semantics.


## Draft decision checkpoint — 2026-09-20

Delivered:

- Explicit selection of an imported season/source, with manual corrections taking
  precedence, followed by one atomic four-record projection publication. New owned
  routes are included in the 13-route cross-user isolation sweep.
- Saved values carry a run ID, calculation timestamp and normalized scoring profile.
  PostgreSQL selects the latest run before loading the pool; another league or a
  player absent from the newly selected pool cannot leak old values into it.
- A scoring edit hides obsolete values until explicit recalculation. Historical
  rows remain unchanged. Legacy values without publication metadata require
  recalculation; legacy observation links are not guessed or backfilled.
- Projection detail follows the exact saved value → adjustment → baseline →
  observation chain. Random IDs only break equal-timestamp ties deterministically.
- React shows the top five server-ranked candidates, confidence, evidence and
  visible risk text, including unverified context. Picks and undo refresh advice;
  ordinary polling does not repeatedly persist recommendations. Full decomposition
  remains available from each shortlist player.
- Compact signed-in header and a fixed-height shortlist preserve the space for
  the player table during re-ranking. The calculation controls explicitly say
  that recent-form/heat rankings are not connected yet.

Validation findings and fixes: missing scoring-engine DI registration, fixture
parser-version labels, an EF grouping/order translation error, and the selector's
accessible name were corrected. The older HTTP lifecycle test now asserts that
scoring changes invalidate existing values and then explicitly recalculates before
requesting the new decomposition. No safety rule or test threshold was weakened.

Targeted tests passed for application orchestration, transaction rollback,
independent league/run reads, manual precedence, scoring invalidation, owned-route
isolation, HTTP publication, pick/undo and the browser journey. Final `bash scripts/gate.sh` passed **243 tests**: 92 Domain, 35 Application,
116 Integration. Build: zero warnings/errors. Domain coverage **89.22%**,
Application **74.33%**. Both the existing D-14 browser gate (0.000px) and the new
React row-stability assertion passed. All five axe scans were clean; mobile
390px and 320px did not overflow. **75 OKF concepts** validated. Toolchain remains
.NET SDK 10.0.302 / Node 24.13.0 / npm 11.6.2; final log:
`/tmp/fb-publication-gate2.log`.

Commands: `dotnet ef migrations add ProjectionPublication` generated the forward
migration without applying it to a developer database; `dotnet format
--no-restore`, TypeScript/Vite builds, focused `dotnet test` runs, and the full
gate completed. The attempted EF `migrations remove --offline` option was not
supported; only this session's uncommitted generated files were regenerated
from the committed snapshot. No prior migration or developer database was edited.

Usage was checked at startup, after implementation, during regression and before
wrap-up. The tool again returned no active goal or remaining-token/quota values;
no numeric account-usage estimate is available. This checkpoint completes the
projection/draft-decision slice, not the whole React migration or heat pipeline.

## Completed-game storage checkpoint — 2026-09-21

Delivered:

- A pure saved-HTML box-score parser with one canonical counting-column map,
  raw-page SHA-256, visible/comment-wrapped full-game tables, DNP/played-zero
  distinction, and explicit malformed-schema/identity/stat failures.
- Immutable completed-game snapshots and a forward EF migration, with full
  page/player provenance, canonical player/game foreign keys and explicit phase.
  These are shared NBA reference rows; no existing ownership changed.
- Atomic page publication and idempotent imports, including a forced concurrent
  insert race. Corrections retain history but current reads select one complete
  page per source/game before phase/date filtering. Removed players cannot leak
  from earlier versions. Failed imports roll back and detach attempted rows.
- BS-01–BS-10 tests and routed contracts. Targeted verification passed after
  correcting a fixture that unintentionally assigned equal timestamps to two
  successive corrections. The deterministic ID tie-break behavior was unchanged.

The parser fixture is synthetic, not a captured real provider page. No live games
were fetched or imported, no worker/identity resolver was added, and heat is not
shown in React yet. The stable scraping allowlist conflict remains unresolved.
Stored samples are current corrected observations through a game date, not an
as-known-at backtesting dataset. Unknown phase is never assumed regular season.

Usage checks returned no active goal or quota/remaining-token report. The earlier
account-limit rejection no longer blocked the authorized test retry. Validation
runs only used disposable PostgreSQL 17; no developer database was migrated.
Final `bash scripts/gate.sh` passed **271 tests** (93 Domain, 35 Application,
143 Integration), with zero build warnings/errors. Domain coverage **89.20%**,
Application **74.33%**. Playwright's real-cookie React journey and all five axe
scans passed, including 390px/320px layouts and row stability after re-ranking.
D-14 measured **0.000 CSS px** movement. **77 OKF concepts** validated.
Log: `/tmp/fb-boxscore-gate.log`; review report/screenshots use the existing links
in the verification ledger above. No frontend visual changes were made in this
checkpoint, and these checks do not replace the owner's visual acceptance.

Commands completed: generated the forward `CompletedBoxScores` migration,
`dotnet format --no-restore`, focused parser/domain/storage tests, local review of
parser validation, correction selection, append-only enforcement and the schema,
and the full gate. No prior migration or developer database was modified.

Next: authenticated league-specific heat query and React best/hot views, consuming
regular-season stored appearances with explicit season/source/through-date,
visible evidence and insufficient-history states. Preserve the accepted latest-3
versus preceding expanding-10-to-30/rolling-30 policy. Until verified game ingestion
exists, production must honestly show missing observations; synthetic test games
must never populate the user's player rankings.

## Recorded performance checkpoint — 2026-09-21

Delivered:

- Authenticated owned `performance-pools` and paged `performance` GET endpoints.
  Explicit season/source/date/view, current saved league scoring, no-store responses,
  category conflict, request validation and no write side effects. The cross-user
  sweep now covers 15 routes; anonymous performance requests return 401.
- Best performing, above-baseline and all-observed views. The C# calculator owns
  ordering, scoring and windows. Responses include policy/model, sample evidence,
  qualified counts, the exact scoring rules used, and latest recorded appearance/
  retrieval dates.
- React selection, paging, game-by-game evidence/provenance, insufficient/empty
  states and retry. The section sits below the draft player table, preserving its
  density and position. Historical results are explicitly dated and completeness/
  freshness unverified. Zero and negative scoring remain legitimate appearances.
- Unit tests, PostgreSQL/HTTP tests and a real-cookie Playwright journey cover
  scoring edits, separate ranks, DNP exclusion, date/source/season isolation,
  pagination, authorization, browser evidence and failed-read recovery.

The initial browser check exposed an ambiguous season selector accessible name;
the explicit label fix passed the repeated journey. Targeted HP verification
passed 2 Application and 6 Integration cases. The prior parser/storage tests
remain in the full gate. No scraping policy was changed and no production game
observations were created. Importer/live-source verification remain outstanding.

Usage checks again exposed no quota or remaining-token report. Tests only used
throwaway PostgreSQL and synthetic fixtures.

Final `bash scripts/gate.sh` passed **277 tests**: 93 Domain, 37 Application,
147 Integration. Build had zero warnings/errors. Domain coverage **89.20%**,
Application **75.21%**. All six axe scans passed, including open evidence and
390px/320px layouts; no document overflow. The draft row-stability assertions
passed and D-14 measured **0.000 CSS px** movement. **79 OKF concepts** validated.
Final log: `/tmp/fb-performance-final-gate.log`.

The first full gate also passed; final review then added the exact scoring-rule
snapshot to the API and evidence panel, with HTTP/browser assertions and a second
full passing gate. Desktop and narrow-layout screenshots were visually inspected.
Owner visual acceptance remains pending. No numeric account quota was available.

The working query/UI is now ready for review against the synthetic fixtures. A
production import still needs explicit resolution of the stable scraping policy's
path-table/prose discrepancy, verified saved-page compatibility, canonical player
and schedule/final-status checks, resumability and visible progress. No permission
to fetch an unlisted path has been inferred.

Visual review artifacts (fictional test players):

- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/performance-desktop.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/mobile-390.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/mobile-320.png`
- `tests/FantasyBasketball.IntegrationTests/TestResults/react-review/report.json`
