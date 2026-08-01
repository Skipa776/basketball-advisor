# E13 — Interface and experience

**Requirements:** R23 · **Depends on:** E05 (the design system it composes from)

**This is the current objective.** Every other epic adds a capability. This one
adds none: it takes what already ships and makes the path through it finishable,
coherent, and worth using. Nothing here changes what the app can compute.

## Why this epic exists

E05 landed the vocabulary — tokens, two themes, sixteen components, a green
accessibility gate. Then four defects survived that green suite, all of the same
shape: each page is individually correct and the *sequence* is not.

- `/welcome` has no inbound link. The route sweep renders it, so every test
  passes, and no user can reach it.
- The landing page's "Create account" leads to "Registration is closed" on a
  default instance.
- Context Review asks for a context event ID that no screen prints.
- `site/index.html` is a second front page for a differently-named product.

None of those is a component bug. They are all path bugs, and no row in the
matrix was looking at the path.

The scope then widened, because the interface was blocking functional testing:
the instrument had six routes and no shell worth the name, and the palette had
no basketball in it. This epic now carries the full overhaul.

## Scope, in order

**0. Amendments.** Three rules had to bend, each recorded where it lives:
designed shells are permitted (`web_ui_blazor.md`, `AGENTS.md`), licensed raster
imagery is permitted on marketing and auth surfaces (`PRODUCT.md`), and league
standings are named as deferred (`post_mvp_roadmap.md`). New rows `D-33` and
`D-34` make each amendment mechanical rather than a promise.

**1. The hardwood palette.** All thirteen colour tokens across four sets —
light, dark, the `prefers-color-scheme` block, and the `.on-dark` island — move
to maple, court cream, and deep brown-black, with basketball orange kept as the
accent. Test-driven: `D-11` and `D-22` name the failing pair and the ratio, so
the loop is edit, run, adjust. `DESIGN.md`'s tables are rewritten with computed
ratios, never estimates.

**2. The shell.** Ten routes need grouping — Decide, League, Data — plus a
utility bar carrying the active-league selector, sync state, theme, and account.
The selector is backed by `LeagueService.ListAsync` and persisted in a cookie,
which also retires the pasted League ID that rows `D-32` fails today.

**3. Close the path.** Rows `D-28`, `D-29`, `D-32`. `/welcome` gets an entry
point. The landing CTA learns whether registration is open. The override form
becomes reachable from the event it overrides.

**4. The surfaces.** Login, My Leagues, My League, Draft Prep Room, Players,
Context Review, Data Sources — redesigned against the new system. Then three
**designed shells** for capabilities that do not exist: Trade Analyzer (R15),
Free Agents (R14), Leaderboard (standings, deferred). Row `D-33` keeps them
honest: name the requirement, render no numbers.

**5. One front door.** Row `D-31`. `site/index.html` either dies and redirects
to the app's landing page, or is rebuilt in the same world.

**6. Phone width.** Row `D-30`, 390 CSS px, every page. Marketing may reflow;
the board keeps its density and its layout stability.

## Boundaries

- **The instrument is not in scope for beautification.** Density, the draft-board
  interaction budget, and every accent ration in `design_system_contract.md`
  survive unchanged. The board is a tool used under a clock; it is not a surface
  to make prettier. `D-14` is the row that proves it.
- **No CSS framework, no component library.** Refused twice, on arithmetic rather
  than taste — see `assumptions.md`. `D-10` fails on import and `D-11` goes blind.
- **No new capability.** If a fix requires a new engine, import, or number, it
  belongs in another epic. The shells exist precisely so that stays true.
- **The Trade Analyzer does not arbitrate fairness.** Judging a trade for the
  other side is permanently out of scope. The surface reports what a trade does
  to *your* team.
- **No NBA, team, or identifiable player likeness**, in any medium. The two
  player photographs in `inspiration-resources/` are mood reference and cannot
  ship.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E13 in this repository: the full interface overhaul.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R23),
   PRODUCT.md, DESIGN.md, and context/AGENT_CONTEXT_INDEX.md. Then read, in full:
     context/codebase_okf/components/web_ui_blazor.md
     context/codebase_okf/contracts/design_system_contract.md
     context/codebase_okf/components/design_system.md
     context/codebase_okf/tests/test_matrix_ui_design.md
     context/codebase_okf/tasks/run_design_process.md
   This is Blazor Server: .razor components and scoped .razor.css. There is no
   .cshtml, no Bootstrap, and no site.css. Do not add any.

2. Write the seven new gate rows FIRST, and watch them fail:
   D-28  every @page route reachable from an in-app link or nav entry
   D-29  every anonymously-rendered CTA lands somewhere reachable in this config
   D-30  no horizontal overflow at 390 CSS px, every page
   D-31  the app landing page and site/index.html name the same product
   D-32  every identifier input is a picker or names where the value comes from
   D-33  every unbacked page names its requirement and renders no numbers
   D-34  every binary under wwwroot/img has an ASSETS.md entry
   All but D-30 are AngleSharp or file assertions in the integration suite.
   D-30 belongs in scripts/ui-browser-gate.mjs beside D-14.

3. Then work the scope order above: palette, shell, path, surfaces, front door,
   phone width.

4. Boundaries, non-negotiable: no CSS framework and no component library; no new
   capability, engine, or import; the draft board's density, accent ration, and
   layout stability (D-14) unchanged; no NBA or player likeness in any asset.
   Every existing row D-10 through D-27 stays green.

5. Walk every route in a browser before calling this done, signed in and signed
   out, both themes, at desktop and 390 px. Four defects survived a green suite
   because nobody made the second request; this epic exists because of them.

Exit gate: scripts/gate.sh green, rows D-10 through D-34 passing, every route
reachable, zero accessibility violations in both themes, ASSETS.md complete, and
a screenshot of each surface at desktop and 390 px attached to the commit
message. Commit conventionally after each scope item.
```
