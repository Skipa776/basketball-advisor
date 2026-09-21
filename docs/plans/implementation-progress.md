# React and player intelligence implementation progress

Updated: 2026-09-20. Accepted decisions and full scope are in
[the execution plan](react-api-player-intelligence-execution.md).

## Current checkpoint

This is a working React/API vertical slice, **not the completed migration or
player-intelligence implementation**. Existing visual studies and Blazor routes
are preserved.

| Milestone | Status | Delivered / remaining |
|---|---|---|
| M0 — baseline | Complete | SSH.NET security repair; approved scope/toolchain recorded; baseline gate green |
| M1 — React foundation | Partial | Pinned React/TypeScript/Vite project, .NET-served `/app`, typed API boundary, loading/error states. Continuous animated landing and complete design-system port remain |
| M2 — basic features | Partial | Account/session/registration availability, explicit editable ESPN setup, owned league selection, player search/detail, snake draft creation, keyboard pick, undo and URL-based reload. League-specific recalculation, ranked advice/evidence and pick/undo reranking now connected. Saved-draft listing and league settings editing remain |
| M3 — verification | Partial | Real HTTP and PostgreSQL tests; cross-user isolation; browser journey, axe scans, desktop/mobile screenshots. Full parity matrix and owner visual acceptance remain |
| M4 — game data | Partial | ESPN setup catalog and independent scoring golden delivered. Per-game schema/parser/import/progress and data sufficiency remain |
| M5 — heat | Partial | Pure C# calculation and best/hot ranking logic pass 20 offline cases; game-data storage, API and UI integration remain |
| M6 — player intelligence UI | Partial | Draft-value shortlist/evidence/decomposition delivered; separate observed best-performing and hot views remain |
| M7 — release | Pending | Remaining routes, distribution, full regression and owner review |

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

Implement **per-game persistence and an offline box-score parser**, then connect
stored appearances to the tested heat calculator and its authenticated query/UI.
Read the routed game-data, heat, provenance, tenancy and scraping concepts first.
The projection publication and draft recommendation flow is now connected; the
remaining heat work must not treat season projections as observed game logs.

Keep the accepted heat rule unchanged: first complete comparison after appearance
13; latest three excluded from the comparison baseline; baseline grows from ten
to thirty and then rolls at thirty; DNP is not zero; reset at season boundaries.
Live box-score fetching remains blocked by the recorded safety allowlist mismatch.
Offline parser/engine work can proceed without weakening that policy. Sleeper
adapters and league automation remain deferred.

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
