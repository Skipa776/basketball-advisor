---
type: component
title: Design System
description: The Blazor component library built from tokens, how DESIGN.md output is ported into it, and the accessibility gate.
tags: [component, ui, design, blazor]
source_paths: [src/FantasyBasketball.Web/src/workspace.css, DESIGN.md]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [../contracts/design_system_contract.md, web_ui_react.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: medium
done_criteria:
  - Every page is composed from inventory components; no page defines its own styles.
  - The a11y gate is green on every page in both themes.
---

# Responsibility

Owns the Blazor component library. Tokens, inventory, interaction budget, and the
accessibility floor are owned by
[design_system_contract](../contracts/design_system_contract.md); the aesthetic by
`DESIGN.md`.

# Where DESIGN.md comes from, and how it lands here

`DESIGN.md` is produced by the interactive process in
[run_design_process](../tasks/run_design_process.md) — the part that needs a human
reacting to visuals. It supplies the palette, typefaces, and personality; this
component supplies the structure that consumes them.

The port is mechanical and one-directional:

1. `DESIGN.md`'s decisions become **values in `Tokens.razor.css`**. Nothing else.
2. Any static HTML/CSS produced during exploration (for example by `/design-html`)
   is a **reference artifact, not shipped code** — its markup is re-expressed as
   Blazor components. Pasting generated HTML into `.razor` files is how a
   parallel styling vocabulary gets in.
3. Component structure does not change to match a mockup. If a mockup needs a
   component the inventory lacks, the inventory changes first, in the contract.

# Design

- One component per inventory entry, scoped CSS, tokens only.
- Components are **presentational**: they take data and render it. No service calls,
  no computation. `EvidenceList` renders evidence; it never decides what counts as a
  risk.
- `PlayerTable` is virtualized. Fifty rows is fine unvirtualized and the full pool is
  not, and the draft board renders the full pool.
- `ThemeToggle` persists the choice; the default follows `prefers-color-scheme`.
- No component library and no CSS framework, per the contract.

# The gate

`accessibility_violations_allowed = 0`. An automated scan runs against every page in
both themes as part of the gate, plus the targeted assertions the contract names
(contrast, keyboard path, layout stability, reduced motion, live-region scope).

Automated scanning catches roughly the mechanical half of accessibility. The
contract's specific rows exist because the half it misses — announcing the *right*
things, not shifting the board under the cursor — is the half that decides whether
this is usable during a live draft.

# Invariants

- **No hardcoded design values outside the token file.** *Check:
  [test_matrix_ui_design](../tests/test_matrix_ui_design.md) row D-10.*
- **Components are presentational** — no service or repository dependency in
  `Components/Design`. *Check: row D-19.*
- **Every page composes inventory components** and defines no page-level styles.
  *Check: row D-20.*
- **The a11y scan is clean on every page, both themes.** *Check: row D-18.*
- **`PlayerTable` is virtualized.** *Check: row D-21 renders the full pool and
  asserts a bounded DOM node count.*
- **Generated exploration HTML is not shipped** — no file under `Components/`
  originates from a design tool. *Check: review, per the change procedure.*

# Change procedure

Adding a component: the contract's inventory first, then the component, then its a11y
row. Changing a token: the contract plus a contrast re-check — every token change is
global. Changing the aesthetic: `DESIGN.md` and token values only; no component
changes.

# Verification

[test_matrix_ui_design](../tests/test_matrix_ui_design.md), rows D-10, D-18 through
D-21.

The incremental React workspace is owned by [web_ui_react](web_ui_react.md).
Its axe browser checks complement the existing Blazor gates; the broader animated
landing and complete token/component migration are still pending.

## Blazor retired — 2026-09-23

The owner retired the Blazor UI in favor of the React app
(`src/FantasyBasketball.Web`). The Razor component inventory, its token file
and the static checks that read them were deleted, so this concept drops to
`partial`: React uses its own CSS custom properties in `workspace.css` and no
component inventory yet. Rows re-covered in React are listed in
[test_matrix_ui_design](../tests/test_matrix_ui_design.md).
