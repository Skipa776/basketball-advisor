---
type: component
title: React Draft Workspace
description: Incremental React migration, same-origin API ownership, and tested account-to-draft flows.
tags: [component, ui, react]
source_paths: [src/FantasyBasketball.Web, src/FantasyBasketball.Api/ApiHost.cs]
test_paths: [src/FantasyBasketball.Web/tests/workspace.mjs, tests/FantasyBasketball.IntegrationTests/Auth/AuthHttpTests.cs]
depends_on: [../contracts/api_surface.md, ../contracts/auth_tenancy_contract.md, ../contracts/design_system_contract.md, web_ui_blazor.md]
status: partial
last_updated: 2026-09-20
owners: [engineering]
---

# Responsibility

Owns the incremental React workspace at `/app`, authorized by the owner in
[the execution plan](../../../docs/plans/react-api-player-intelligence-execution.md).
Existing Blazor pages remain reachable. The dashboard links into React and React
links back to the existing app and data sources; neither is an orphan route.

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
  styles during migration; existing Blazor tokens and interaction gates remain.
- Table rows stay in place after a pick. The keyboard search/pick path restores
  focus, values show server evidence, and unverified context is visibly labelled.

# Build and verification

Pinned dependencies live in `package.json`/`package-lock.json` and are mirrored in
`stack_config.toml`. The gate installs them using `npm ci --ignore-scripts`, then
builds before .NET so static asset discovery includes the generated bundle.
Generated `wwwroot/app` assets are ignored. `/app` is served by the .NET host;
API authorization applies independently of access to the public JavaScript.

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
