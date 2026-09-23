---
type: component
title: Web UI (Blazor Server) — retired
description: Tombstone. The Blazor Server UI was retired on 2026-09-23 in favor of the React app; this records what it was and where its history lives.
tags: [component, ui, blazor, retired]
source_paths: []
test_paths: []
depends_on: [web_ui_react.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: low
---

# Status

**Retired 2026-09-23 by the owner.** The React app
([web_ui_react](web_ui_react.md)) is the only user interface. `status: partial`
is a validator limitation (there is no `retired` value); nothing here is built
or planned.

# What was removed

- `src/FantasyBasketball.Api/Components/` — every Razor page, the design
  component inventory and its token stylesheet.
- `wwwroot/carousel.js`, `wwwroot/draft-board.js`, `wwwroot/theme.js`,
  `wwwroot/fonts/` and `scripts/ui-browser-gate.mjs` (the Blazor D-13/D-14/D-30
  browser gate).
- Razor/interactive-server registration and the `/_blazor` transport.
- HTML form-post endpoints `/account/{register,login,logout}/submit` and
  `/account/active-league/submit`, the `ActiveLeague` cookie, the demo-content
  landing flag (`Demo:Enabled`, `dev.sh --demo`) and the unused Google sign-in
  options.

`/` now serves the React index, like `/app`. Unauthenticated page requests
redirect to `/app`; `/api/*` still answers `401`.

# History

The full Blazor tree is in git at commit `936393d` and earlier. Decisions that
cited this concept (epics E03, E05, E13, the liquid-glass migration plan and
`assumptions.md`) are historical records and keep their links here.
