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
| M2 — basic features | Partial | Account/session/registration availability, explicit editable ESPN setup, owned league selection, player search/detail, snake draft creation, keyboard pick, undo and URL-based reload. Saved-draft listing, league settings editing and ranking/recommendation presentation remain |
| M3 — verification | Partial | Real HTTP and PostgreSQL tests; cross-user isolation; browser journey, axe scans, desktop/mobile screenshots. Full parity matrix and owner visual acceptance remain |
| M4 — game data | Partial | ESPN setup catalog and independent scoring golden delivered. Per-game schema/parser/import/progress and data sufficiency remain |
| M5 — heat | Pending | Latest three appearances versus the preceding expanding 10–30 appearance baseline; no heat number is presented as implemented |
| M6 — player intelligence UI | Pending | Separate best-performing, draft-value and hot views with evidence, samples and dates |
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
remaining-usage claim is possible. The owner requested continuation after the green React checkpoint. Begin the
heat calculation as a separate tested step; do not leave partially tested work
when the context budget approaches its limit.

The earlier automatic-review account-limit block was cleared by the owner's
continuation; restore and subsequent approved tooling commands ran successfully.

## Single next action

Complete **draft decision presentation and its integration test**: ensure a newly
created league can obtain/recompute its league-specific projections, show the
server-ranked recommendations with evidence, and verify re-ranking after pick
and undo. The current player pool displays existing draft values but is not yet
that full ranking-first surface. Then implement the per-game/heat subsystem,
reading its routed contracts and safety concepts before editing.

Keep the accepted heat rule unchanged: first complete comparison after appearance
13; latest three excluded from the comparison baseline; baseline grows from ten
to thirty and then rolls at thirty; DNP is not zero; reset at season boundaries.
Live box-score fetching remains blocked by the recorded safety allowlist mismatch.
Offline parser/engine work can proceed without weakening that policy. Sleeper
adapters and league automation remain deferred.
