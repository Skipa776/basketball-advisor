---
type: task
title: Run the Design Process
description: Which design skills to run, in what order, what each produces, and which steps need a human's taste rather than an agent.
tags: [task, design, ui, workflow]
source_paths: [DESIGN.md, DESIGN_BRIEF.md, src/FantasyBasketball.Web/src/workspace.css]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [../contracts/design_system_contract.md, ../components/design_system.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
---

# Responsibility

Owns the *process* that produces `DESIGN.md`. The structural half of the UI is
already decided in
[design_system_contract](../contracts/design_system_contract.md) — tokens,
inventory, interaction budget, accessibility floor. What is left is the aesthetic,
and that cannot be decided by an agent reading a spec, because it is settled by a
person looking at options and reacting.

**Split, plainly:** structure is specified; taste is elicited. Steps below marked
**you** need your reaction to visuals. Steps marked **agent** do not.

# Order

## 0. Read the brief — *agent, done*

`DESIGN_BRIEF.md` at the repo root is the input to step 1. It states what the
product is, who uses it, the four surfaces that matter, the constraints that are
already non-negotiable, and the aesthetic questions that are genuinely open. It
exists so the consultation starts from the real problem instead of interviewing you
about basics the repo already knows.

## 1. `/design-consultation` — *you*

Produces `DESIGN.md`: aesthetic direction, typography, colour, spacing, motion,
plus font and colour preview pages to look at.

Feed it `DESIGN_BRIEF.md`. The questions worth spending your judgment on:

- **Density versus comfort.** The draft board is the hardest surface in the app and
  it is a table under time pressure. How dense before it stops being readable?
- **Where does it look at 11pm?** Dark mode is required, not a variant — a draft
  happens at night. Design it as a first-class theme, not an inversion.
- **Trustworthy, not slick.** This tool tells you a projection is uncertain. An
  aesthetic that reads as marketing undermines the one thing the product is
  claiming.

## 2. `/design-shotgun` — *you*

Generates several variants and a comparison board, and collects structured
feedback. Run it on the **Draft Assistant** first, not the Dashboard: it is the
densest and most constrained surface, and a system that survives it will handle the
rest. A system designed on the Dashboard and then applied to the board tends to
fall apart on contact.

## 3. `/plan-design-review` — *you, briefly*

Rates the design plan per dimension and fixes the plan before implementation. Cheap
here, because the alternative is discovering a density or hierarchy problem after
fifteen Blazor components exist.

## 4. Port into components — *agent*

Per [design_system](../components/design_system.md):

- `DESIGN.md`'s decisions become **values in `Tokens.razor.css`** and nothing else.
- Any static HTML from `/design-html` is a **reference artifact, not shipped code** —
  it emits Pretext-native HTML/CSS, and this app renders Blazor. Re-express the
  markup as components; do not paste it in. A second styling vocabulary next to the
  tokens is the failure this step exists to avoid.
- Component structure does not bend to a mockup. A mockup needing a component the
  inventory lacks means the inventory changes first, in the contract.

## 5. Charts — *agent*

Load the **`dataviz`** skill before writing any chart. Four visualizations are
planned, and each is easy to get wrong:

| Chart | Trap to avoid |
|---|---|
| Category profile (radar or bar) | Radar charts flatter balanced rosters and hide a punt; prefer bars |
| Trend sparkline per player | Unlabelled sparklines with independent y-axes are not comparable across rows |
| Projection decomposition | A stacked bar mixing baseline and adjustment reads as one total |
| Back-test decile calibration | Needs the identity line or the deviation is invisible |

Series colours come from design tokens, not the library's defaults.

## 6. `/design-review` on the running app — *agent, then you*

Visual QA against the live UI: spacing inconsistency, hierarchy, AI-slop patterns,
slow interactions. Fixes iteratively with before/after screenshots.

Run it **after** the a11y gate is green, not instead of it. They catch different
things: the gate catches contrast and keyboard traps; the review catches a board
that is technically accessible and still unpleasant to scan.

## 7. `/benchmark` on the Draft Assistant — *agent*

The interaction budget in the contract includes a perceived-100ms re-rank and no
layout shift. Measure it against a full player pool. This is the one design
requirement that is a performance requirement.

# What not to do

- **Do not start at step 4.** Porting an aesthetic nobody chose produces a generic
  UI that is expensive to change later, because by then it is in fifteen components.
- **Do not skip step 0.** A consultation that has to interview you about what
  fantasy basketball is wastes the part of the process only you can do.
- **Do not treat the a11y gate as the design bar.** Zero violations is the floor.
- **Do not let a design skill edit `src/FantasyBasketball.Web/src/`.** Its output is `DESIGN.md`, token
  values, and reference artifacts. An agent ports; a design tool proposes.

# Verification

[test_matrix_ui_design](../tests/test_matrix_ui_design.md). `DESIGN.md` existing is
the exit condition for steps 1–3; rows D-10 … D-21 green is the exit condition for
steps 4–7.
