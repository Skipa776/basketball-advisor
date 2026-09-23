---
type: component
title: React Draft Workspace
description: Incremental React migration, same-origin API ownership, and tested account-to-draft flows.
tags: [component, ui, react]
source_paths: [src/FantasyBasketball.Web, src/FantasyBasketball.Api/ApiHost.cs]
test_paths: [src/FantasyBasketball.Web/tests/workspace.mjs, tests/FantasyBasketball.IntegrationTests/Auth/AuthHttpTests.cs]
depends_on: [../contracts/api_surface.md, ../contracts/auth_tenancy_contract.md, ../contracts/design_system_contract.md, web_ui_blazor.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
---

# Responsibility

Owns the incremental React workspace at `/app`, authorized by the owner in
[the execution plan](../../../docs/plans/react-api-player-intelligence-execution.md).
Since 2026-09-23 it is the only UI: the owner retired Blazor, and `/` serves the
same React index as `/app`.

# Boundaries

- React formats server results; scoring and draft ranking remain in C#.
- Use the existing API envelope, same-origin secure cookies, and a fresh
  anti-forgery token for each mutation. Never store credentials or private
  account data in local storage. Only league/draft identifiers appear in URLs.
- Cancel obsolete reads. Never retry a mutation automatically. Reload persisted
  draft state after success or conflict, and periodically refresh a visible tab.
- The server setup catalog owns scoring values and enum vocabulary. ESPN is an
  explicit editable choice; an empty scoring request is still invalid.
- No CSS framework/component library. `workspace.css` owns the React surface's
  styles.
- Table rows stay in place after a pick. The keyboard search/pick path restores
  focus, values show server evidence, and unverified context is visibly labelled.

# Build and verification

Pinned dependencies live in `package.json`/`package-lock.json` and are mirrored in
`stack_config.toml`. The gate installs them using `npm ci --ignore-scripts`, then
builds before .NET so static asset discovery includes the generated bundle.
Generated `wwwroot/app` assets are ignored. `/app` is served by the .NET host;
physical bundles are public static files even when a running host sees a new
Vite hash after a rebuild. API authorization applies independently of access
to the public JavaScript. An anonymous HTTP test writes a new bundle after host
startup and checks its JavaScript MIME type to catch a blank landing page.

The Playwright test runs inside the real-cookie HTTP test fixture against its
throwaway PostgreSQL database. External browser requests are blocked. It covers
registration availability, anti-forgery rejection, explicit league setup, keyboard
pick, persisted reload, undo, logout/login, axe accessibility and mobile overflow.
See [implementation progress](../../../docs/plans/implementation-progress.md) for
actual latest results and screenshot locations, rather than inferring success
from the existence of tests.

# Remaining work

This is a functional migration slice, not full parity. The accepted continuous
animated landing remains in the visual prototype. League settings edits,
saved-draft listing, import/context/account-management
parity, and the per-game heat pipeline remain on the execution plan. Do not
promote this concept to implemented until the accepted migration gates pass.

## Draft decision slice — 2026-09-20

The points workspace now offers an explicit imported season/source selection
and atomic projection recalculation. It shows the first five server-ranked
recommendations, server-named confidence and evidence, visible risk text, and
the four-part projection detail. Advice refreshes after a pick, undo,
recalculation or explicit refresh; it is not repeatedly persisted by polling.
The shortlist has a fixed-height scroll area and loading text reserves space
so re-ranking does not move the player search/table. The signed-in header is
compact. The browser journey now covers calculation, evidence/decomposition,
recommendation removal/restoration, and player-row stability as well as auth.

## Recorded performance slice — 2026-09-21

An expandable section below the player pool offers explicit recorded season/source
and game-date selection, best/above-baseline/all views, pagination and retry.
It renders server-calculated scores and policy, actual game evidence and provenance,
and separate insufficient/empty states. It is read-only and has no draft-pick
shortcut or effect on the main board's density. Names use the existing player API
for the visible ten rows. Obsolete reads cancel and league changes reset selection.
Historical data is labelled as such; completeness and freshness are unverified.
HP-05 covers the real-cookie browser journey, error recovery and evidence at
1440px/390px/320px with axe scans. The data importer and visual migration remain
partial; test fixtures are not production observations.

## Visual refresh — 2026-09-22

`/app` adopts the owner's portfolio (github.com/Skipa776/portfolio) visual language,
rebuilt in plain CSS plus one scroll listener in `Landing.tsx` (no framework, no
animation library): white page, pill buttons with a rising hover fill, hairline
rows, and a dark closing section. Signed-out visitors get falling headline letters,
a word slide-up pitch, two scroll-drifting photo strips, magnetic round CTAs and a
curved edge into the dark sign-in section. Motion stops under reduced motion. The
signed-in intro stays compact and the board keeps its row height. Photos are CC0 or
public domain and attributed in `ASSETS.md` (row D-34). Copy is shortened, but the
unverified/estimate labels and every string the browser journey checks are kept.

## Functional page checkpoint — 2026-09-22

React navigation now reaches owner data sources, saved drafts, league settings,
context review and account data through `/app/{page}`. Trade, streaming and
standings destinations state the owning requirement or deferred roadmap entry
and show no player/team values. Category league setup is available; the main
draft interface remains points-only and says so for category leagues.

New pages use the same-origin API client, cookie anti-forgery for mutations and
no local storage. Import controls queue work only after an explicit click; tests
do not invoke them. Saved drafts are listed under a selected league and reopen
through the persisted session URL. Settings preserve the scoring endpoint and
explicit recalculation step; structural edits are rejected after draft creation.
The ownership and API behavior are specified in
[`api_surface`](../contracts/api_surface.md).

The host validates the required BallDontLie key at startup under the stable
secrets policy. The React source page shows run and freshness status after a
valid configuration has booted; it does not imply that a missing-key state is
reachable inside a running app.

The 2026-09-22 full gate passes 280 tests with loopback Kestrel, disposable
PostgreSQL, and the browser journey. The live local `/app` also renders after a
frontend rebuild with no browser errors. This component remains partial because
live game ingestion was still outstanding (Blazor was later retired, 2026-09-23).

## Projected players and routing — 2026-09-22

`/app/projections` lists a points league's current published values ranked by
projected season points, 50 per page, with player detail on click. Category
leagues get a stated limitation, not points values. `/app/` and `/app/{page}/`
now route like their bare paths; before this, signing in at `/app/` showed
"Page not found". The browser journey covers both and the ranking order.

## Landing data — 2026-09-23

The signed-out landing keeps the portfolio-derived hero (falling letters, word
reveal, magnetic round buttons, curve and dark join section) and replaces the
court photo strips with live reference data from `/api/public/*`
(`src/landingData.tsx`):

- **Previous game day** — two scroll-drifting rows of player cards (Commons
  headshot, name, `CAT n/9`, ESPN points, box line). Each row is its own
  keyboard-focusable horizontal scroller, so every card is reachable on a phone.
  A player without a credited photo shows initials; no uncredited image ships.
- **Rising right now** — the risers table; rows rise into place as they scroll
  into view and reset once they leave below, so scrolling back down replays it.
  Reduced motion shows every row in place.
- Photo credits (`src/playerPhotos.ts`, mirrored in `ASSETS.md`) are linked from
  the join section.

Evidence: the `landingChecks` block of `tests/workspace.mjs` (mocked public data,
motion and reduced-motion contexts, axe at 1280/390/320 px, no overflow).

## Signed-in menu — 2026-09-23

After sign-in `/app` is a plain white menu, "What do you want to see?", with one
oval row per destination in the style of the portfolio projects list
(`src/hub.tsx`): Mock draft (`/app/draft`), Teams in the league (league
settings), Waiver wire analyzer (`/app/waiver`: public risers plus the league's
recorded performance for a points league), Projected players, Your drafts,
Trade analyzer, Matchup analyzer, Streaming advisor, Context review, Account
data and, for the instance owner, Data sources. Unbuilt tools carry a "Soon" tag
and open a page that names what they wait on and shows no numbers (row `D-33`;
the phrase "coming soon" is never used). Old `/app?…&draft=` links still open the
draft. The masthead is the wordmark, Menu and Sign out.

"Teams in the league" opens league settings (team count, scoring, roster slots):
there is no per-team roster capability to show. Matchup has no specification
yet; the owner decides what it compares.
