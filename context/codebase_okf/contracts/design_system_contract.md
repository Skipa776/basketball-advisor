---
type: contract
title: Design System Contract
description: Token vocabulary, component inventory, the draft-board interaction budget, evidence and confidence presentation rules, and the WCAG 2.2 AA floor.
tags: [contract, design, ui, accessibility]
source_paths: [src/FantasyBasketball.Web/src/workspace.css, DESIGN.md]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [recommendation_evidence_contract.md, ../components/web_ui_react.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: medium
edit_policy: stable_contract
done_criteria:
  - Every component reads tokens; no component hardcodes a colour or spacing value.
  - Risk and confidence are never conveyed by colour alone.
  - The draft board meets its interaction budget with a keyboard only.
---

# Responsibility

Owns the structural half of the UI: the token vocabulary, what components exist,
how evidence and confidence are presented, and the accessibility floor. The
**aesthetic** half — palette, typography, personality — is owned by `DESIGN.md`,
produced by the interactive design process in
[run_design_process](../tasks/run_design_process.md). This file is what makes that
aesthetic implementable and testable; it deliberately does not choose a look.

Why the split: taste requires a human reacting to visuals, and structure does not.
Everything here can be decided and enforced without knowing what the app looks
like.

# Tokens

One source, `Components/Design/Tokens.razor.css`, as CSS custom properties.
**No component hardcodes a colour, spacing, radius, or duration.**

```text
--color-bg, --color-surface, --color-surface-raised
--color-text, --color-text-muted
--color-accent, --color-accent-ink, --color-accent-contrast
--color-rank                                             # cool tone, rank numbers
--color-positive, --color-negative, --color-caution      # semantic, never decorative
--space-1 … --space-8            geometric scale, one base unit
--text-2xs … --text-hero         type scale + --leading-tight / --leading-normal
--radius-sm / -md / -lg
--motion-fast (120ms) / --motion-base (200ms) / --motion-slow (400ms)
--focus-ring
--font-sans / --font-mono / --font-display
--color-rule / --color-border-strong
--color-surface-wood / --color-court-line       # decorative shell field, R23
--shadow-sm / -md / -lg                         # elevation, R23
--texture-wood                                  # decorative plank field, R23
--board-row-height / --target-min / --content-max
--hero-min-height / --carousel-interval
--sidebar-width / --utility-bar-height          # the app shell, R23
```

**Shadows carry an offset and a soft blur, in two layers.** A tight contact
shadow plus a wide ambient one, because one blur radius cannot do both jobs. A
zero-offset coloured halo is decoration, not depth, and is not what these are
for. They are tinted with the text brown rather than neutral black — a grey
shadow on a warm surface reads as dirt.

**The board gets no elevation.** Rows are separated by hairline rules; a shadow
on a 28px row at fifty rows is visual noise under a clock, and elevation implies
a layering the board does not have.

**`--color-surface-wood` and `--color-court-line` are decorative by
construction.** They carry the hardwood field behind the shell and the court
geometry drawn on it, and **no text or interactive boundary may land on either**.
That is why they are named in `decorativeOnly` rather than in a contrast pair —
the moment a label sits on the wood, it needs a pair and a measured ratio.

**Two accent tokens, not one.** `--color-accent` is held to 3:1 and is for fills,
CTAs and display type; `--color-accent-ink` is held to 4.5:1 and is the only one
allowed to be orange small text. A single orange cannot do both jobs across an
off-white page and a dark panel — the arithmetic is in `DESIGN.md`.

**`.on-dark` is an always-dark island.** It re-declares the full dark token set
regardless of the ambient theme, for surfaces that are dark in both. Anything
rendered inside it is measured against *its* surfaces, not the page's. Row `D-22`.

**`--font-display` never renders a number.** Row `D-27`.

**Light and dark are both required**, via `prefers-color-scheme` plus a manual
override the choice of which persists. Not a preference: a live draft happens at
night, and a self-hosted tool with one hardcoded theme will be used in the wrong
one.

**No CSS framework.** Ten pages of scoped Blazor CSS over a token file does not
justify Tailwind or Bootstrap, and either would put a second styling vocabulary
next to the tokens. Charts use the `dataviz` skill's guidance, with series colours
drawn from these tokens rather than a chart library's defaults.

Re-raised and refused again 2026-07-31. The arithmetic, not the taste: Bootstrap
ships thousands of hex and `px` literals, so row `D-10` fails the moment it is
imported, and there is no way to pass it except by exempting the framework —
which is deleting the row. Row `D-11` computes contrast **from this token file**,
so a second palette it cannot see turns a green accessibility gate into a false
statement. Row `D-20` forbids page-level styling, and utility classes in markup
are page-level styling with extra steps. Full reasoning in
[assumptions](../assumptions.md).

# Two surface classes

The product has two kinds of screen and they do not share a density or an accent
budget:

| | Marketing and auth | The instrument |
|---|---|---|
| Pages | landing, sign in, sign up, `/welcome` | board, players, league, leagues, leaderboard, trade, free agents, review, sources |
| Read | once, while deciding | repeatedly, under a clock |
| Type | display face, hero sizes | system stacks, `--text-sm` rows |
| Accent | used freely | **recommended pick only** (`D-23`) |
| Motion | one paused carousel (`D-24`) | none |
| Imagery | licensed raster permitted, `ASSETS.md` (`D-34`) | player portraits only, via `PlayerAvatar`; no decorative photography |

Both classes draw from the same token file. A component belongs to one class or
the other and does not migrate: the hero has no place on the board, and the board's
28px row has no place on a landing page.

# Density

On the instrument this is a decision-density tool. The draft board shows fifty
rows a user scans under time pressure.

- Tables are the primary layout for player data. Cards are for single-subject
  summaries only.
- Compact row height with generous *horizontal* separation — vertical padding is
  what costs rows on screen.
- Numbers are tabular-figure aligned and right-aligned; a column of misaligned
  decimals is unreadable at speed.
- Projection decomposition (R8) renders as baseline → adjustment → final in one
  row, never three separate panels that require scrolling to compare.

# The draft-board interaction budget

The live draft is the only surface with a hard time constraint: ninety seconds
per pick, and the user is also watching a draft room elsewhere.

| Budget | Requirement |
|---|---|
| Pick entry | ≤ 3 keystrokes to commit a known player: type to filter, arrow, enter |
| Re-rank | Perceived under 100 ms after a pick; the R7 target is the server half |
| **Layout stability** | **The board must not shift under the cursor when it re-ranks** |
| Undo | Reachable by keyboard, one action, no confirmation dialog |
| Focus | Returns to the search field after a pick commits |

Layout stability is the one most likely to be missed and the most damaging: a
board that reflows the instant a rank changes will cause a misclick on the wrong
player during a live draft. Row height is fixed; after a re-rank the scroll
position is corrected so every surviving row keeps its offset inside the
viewport; nothing appears or disappears above that position mid-interaction.

**Rows do not animate into their new rank.** Correcting the scroll is what the
requirement asks for, and a row transitioning `transform` at the same time fights
that correction. The board therefore has no movement for `prefers-reduced-motion`
to reduce — re-rank is instant for everyone, by construction rather than by
media query.

# Evidence and confidence

[recommendation_evidence_contract](recommendation_evidence_contract.md) owns the
data; this owns the presentation:

- **Supporting and risk items are distinguished by icon *and* text label, never by
  colour alone** (WCAG 1.4.1). Colour reinforces; it never carries the meaning.
- Risks are never collapsed behind a disclosure by default. A recommendation whose
  risks are hidden is a recommendation without risks.
- Confidence renders as a labelled chip — `High` / `Moderate` / `Low` /
  `Speculative` — never a bare number and never a progress bar. Four buckets exist
  precisely to avoid implying precision.
- Unverified context is marked inline where it affects a number, not only on the
  Context Review page.
- Stale data shows its age in words (`updated 3h ago`), not a timestamp the reader
  must subtract from now.

# Accessibility floor — WCAG 2.2 AA, gated

| Requirement | Rule |
|---|---|
| Contrast | 4.5:1 body, 3:1 large text and UI boundaries, **in both themes, against every surface the token can land on** — including `--color-surface-raised`, since a bordered control that paints its own raised fill has its boundary measured against that fill, not against the page |
| Keyboard | Every action reachable; the draft board fully operable without a mouse |
| Focus | Visible always; `--focus-ring` is never removed |
| Targets | 24×24 CSS px minimum (WCAG 2.2 2.5.8) |
| Motion | Respect `prefers-reduced-motion`; row animation becomes instant |
| Forms | Every input labelled; errors named and associated |
| Live regions | See below |

**Live-region discipline:** announce the pick that was made and a change in the
top recommendation. Do **not** announce every re-ranked row — a `aria-live`
region on a fifty-row board that recomputes each pick is unusable with a screen
reader, and "we added ARIA" would have made it worse than silence.

`accessibility_violations_allowed = 0` in
[`stack_config.toml`](../../../stack_config.toml). This is a gate, not an
aspiration.

# Component inventory

`PlayerTable`, `PlayerRow`, `StatCell`, `ProjectionDecomposition`,
`EvidenceList`, `ConfidenceChip`, `TrendBadge`, `CategoryProfile`,
`BacktestCalibration`,
`DraftBoard`, `PickEntry`, `SourceHealthCard`, `EmptyState`, `ErrorState`,
`PageHeader`, `ThemeToggle`.

Added by R23 — the shell and the shared instrument language:

`LeagueSelector`, `NotBuiltState`, `PlayerAvatar`, `SearchField`,
`SkeletonRows`, `DraftStatus`.

The sidebar and the utility bar are markup inside `MainLayout`, not components.
They render once, in one place, and have no second caller — extracting them
would buy a file boundary and nothing else. An earlier draft of this list named
them, and `SegmentedControl` and `FilterGroup` besides; all four were removed on
2026-08-03 when the surfaces that were supposed to need them turned out not to.
**This list names what exists.** A contract that promises components nobody
built teaches the reader to check the directory instead of the contract.

`FilterGroup` in particular was dropped for a reason worth keeping: the only
filter it had to offer was position, and positions arrive from the provider as
free text. A fixed PG/SG/SF/PF/C control would return nothing against a pool
that says `G-F`, silently. Search is the filter this app can honestly claim.

`NotBuiltState` is the one that needs justifying. It renders a designed surface
for a capability that does not exist and **names the requirement or roadmap entry
it waits on**, which is what separates it from a placeholder. It is a component
rather than a paragraph for the same reason `DemoDataBadge` is: the sentence that
admits a thing is not built cannot be forgotten by whoever lays out the page
around it. Row `D-33` enforces both halves — the naming, and the absence of
numbers.

`PlayerAvatar` renders initials over a CSS-generated fill, and takes an optional
`ImageUrl` for a portrait whose licence is recorded in `ASSETS.md` (row `D-34`).
**Initials are the default, not the failure case.** No portrait set covers every
player in a league, so a design that assumes one breaks on the first rookie; the
component treats a missing photo as an ordinary state.

`DraftStatus` carries the pick, the round, and whether the reader is on the
clock. It states the turn with fill and weight rather than the accent: orange on
that screen belongs to the recommended row (`D-23`), and a second orange object
above the board spends the signal twice.

`SkeletonRows` renders at `--board-row-height` exactly, so the loaded state
occupies the same geometry as the loading state and nothing shifts on arrival —
the same layout-stability concern as `D-14`, one step earlier in the lifecycle.

Marketing and auth class: `SiteHeader`, `SiteFooter`, `HeroBanner`,
`CourtBackdrop`, `PlatformRow`, `RiserCarousel`, `RankList`, `NewsGrid`,
`AuthPanel`, `OnboardingStepper`, `DemoDataBadge`.

`EmptyState` and `ErrorState` are on the list because a self-hosted app's **first
run is empty** — no players, no league, no imports. That is the state a new user
actually sees first, and it is the one most often left unstyled. `AuthPanel`
counts as a first-run surface for the same reason and satisfies rows D-17 and
D-20 in place of `EmptyState`/`PageHeader`: it renders its own heading, and
requiring both would put two level-one headings on the sign-in page.

`PlatformRow` renders its own no-affiliation line. That text is part of the
component, not of the page, so a third-party name cannot be rendered without it.

`DemoDataBadge` is a component rather than a string for the same reason: the
label that says content is fictional cannot be forgotten by whatever renders the
sample content next.

# Invariants

- **No hardcoded design values in components.** *Check:
  [test_matrix_ui_design](../tests/test_matrix_ui_design.md) row D-10 greps
  `.razor` and `.razor.css` for hex colours, raw `px` spacing, and `ms`
  durations outside the token file.*
- **Contrast passes in both themes.** *Check: row D-11 computes contrast for every
  foreground token against every surface token, and fails if the token file
  defines a colour no pair covers and no exemption names.*
- **Risk is never colour-only.** *Check: row D-12 asserts each evidence item
  renders an icon and a text label.*
- **The board is keyboard-operable and focus returns after a pick.** *Check: row D-13.*
- **The board does not shift on re-rank.** *Check: row D-14 records row offsets
  before and after a pick and asserts no reflow above the scroll position.*
- **Board rows declare no motion to reduce.** *Check: row D-15 asserts no board row
  transitions a geometry property and the scroll correction never animates.*
- **Live regions announce the pick and the top-recommendation change only.**
  *Check: row D-16.*
- **Every page has a styled empty and error state.** *Check: row D-17 renders each
  page with no data. `NotBuiltState` satisfies it for a surface whose capability
  does not exist — there is no data state to reach, and the whole page is a
  styled account of why. Row D-33 stops that being a loophole.*
- **Automated a11y scan is clean on every page.** *Check: row D-18.*
- **Design components are presentational.** *Check: row D-19 asserts no type under
  `Components/Design` takes a service or repository dependency.*
- **Pages compose the inventory and define no styles of their own.** *Check: row
  D-20.*
- **The full player pool renders a bounded DOM.** *Check: row D-21 asserts the
  table virtualizes.*
- **The always-dark island is measured against its own surfaces, and is actually
  dark.** *Check: row D-22 runs every contrast pair inside `.on-dark` and asserts
  it matches the dark set exactly.*
- **Only the recommended row uses the accent on the board.** *Check: row D-23
  splits each accent rule's selector on commas and requires every part to name
  the recommendation.*
- **The carousel is pausable and still under reduced motion.** *Check: row D-24
  asserts the reduced-motion guard, the pause control, hover and focus
  suspension, and that slides do not announce themselves as they rotate.*
- **The landing page renders for a signed-out visitor without reading owned
  data.** *Check: row D-25 requests `/` anonymously and asserts the dashboard's
  owned-data sections are absent.*
- **Sample content is opt-in and labelled fictional.** *Check: row D-26 asserts
  it is absent with the flag off and carries its label with the flag on.*
- **Numbers never render in the display face.** *Check: row D-27.*
- **A surface with no backing capability names what it waits on and shows no
  numbers.** *Check: row D-33.*
- **Every shipped binary is attributed.** *Check: row D-34 pairs `wwwroot/img`
  against `ASSETS.md`.*

# Change procedure

Adding a component: the inventory here, the component, and its a11y row. Changing
a token: this file plus a contrast re-check — a token change is a global visual
change and never a local one. Changing the aesthetic is a `DESIGN.md` change and
does not touch this file.

# Verification

[test_matrix_ui_design](../tests/test_matrix_ui_design.md), rows D-10 through
D-34.

## Owner-authorized React migration

The approved React execution plan introduces a separate `/app` surface while
Blazor remains supported. Its interim styles are owned by `workspace.css` in
`src/FantasyBasketball.Web/src`; the token-file location above remains canonical
for Blazor. No CSS framework or component library is introduced. This changes
the rendering technology and style location, not the accessibility floor or
the requirement to show evidence and unverified-context labels.

## Blazor retired — 2026-09-23

The owner retired the Blazor UI in favor of the React app
(`src/FantasyBasketball.Web`). The Razor component inventory, its token file
and the static checks that read them were deleted, so this concept drops to
`partial`: React uses its own CSS custom properties in `workspace.css` and no
component inventory yet. Rows re-covered in React are listed in
[test_matrix_ui_design](../tests/test_matrix_ui_design.md).
