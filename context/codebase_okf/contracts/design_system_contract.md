---
type: contract
title: Design System Contract
description: Token vocabulary, component inventory, the draft-board interaction budget, evidence and confidence presentation rules, and the WCAG 2.2 AA floor.
tags: [contract, design, ui, accessibility]
source_paths: [src/FantasyBasketball.Api/Components/Design, DESIGN.md]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [recommendation_evidence_contract.md, ../components/web_ui_blazor.md]
status: partial
last_updated: 2026-07-30
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
--color-bg, --color-surface, --color-surface-raised, --color-border
--color-text, --color-text-muted
--color-accent, --color-accent-contrast
--color-positive, --color-negative, --color-caution      # semantic, never decorative
--space-1 … --space-8            geometric scale, one base unit
--text-xs … --text-2xl           type scale + --leading-tight / --leading-normal
--radius-sm / -md / -lg
--motion-fast (120ms) / --motion-base (200ms)
--focus-ring
--font-sans / --font-mono
--color-rule / --color-border-strong
--board-row-height / --target-min / --content-max
```

**Light and dark are both required**, via `prefers-color-scheme` plus a manual
override the choice of which persists. Not a preference: a live draft happens at
night, and a self-hosted tool with one hardcoded theme will be used in the wrong
one.

**No CSS framework.** Six pages of scoped Blazor CSS over a token file does not
justify Tailwind or Bootstrap, and either would put a second styling vocabulary
next to the tokens. Charts use the `dataviz` skill's guidance, with series colours
drawn from these tokens rather than a chart library's defaults.

# Density

This is a decision-density tool, not a marketing site. The draft board shows fifty
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
player during a live draft. Rows animate position over `--motion-base`; row height
is fixed; nothing appears or disappears above the viewport's current scroll
position mid-interaction.

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
| Contrast | 4.5:1 body, 3:1 large text and UI boundaries, **in both themes** |
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

`EmptyState` and `ErrorState` are on the list because a self-hosted app's **first
run is empty** — no players, no league, no imports. That is the state a new user
actually sees first, and it is the one most often left unstyled.

# Invariants

- **No hardcoded design values in components.** *Check:
  [test_matrix_ui_design](../tests/test_matrix_ui_design.md) row D-10 greps
  `.razor` and `.razor.css` for hex colours, raw `px` spacing, and `ms`
  durations outside the token file.*
- **Contrast passes in both themes.** *Check: row D-11 computes contrast for every
  token pair in use.*
- **Risk is never colour-only.** *Check: row D-12 asserts each evidence item
  renders an icon and a text label.*
- **The board is keyboard-operable and focus returns after a pick.** *Check: row D-13.*
- **The board does not shift on re-rank.** *Check: row D-14 records row offsets
  before and after a pick and asserts no reflow above the scroll position.*
- **`prefers-reduced-motion` disables row animation.** *Check: row D-15.*
- **Live regions announce the pick and the top-recommendation change only.**
  *Check: row D-16.*
- **Every page has a styled empty and error state.** *Check: row D-17 renders each
  page with no data.*
- **Automated a11y scan is clean on every page.** *Check: row D-18.*

# Change procedure

Adding a component: the inventory here, the component, and its a11y row. Changing
a token: this file plus a contrast re-check — a token change is a global visual
change and never a local one. Changing the aesthetic is a `DESIGN.md` change and
does not touch this file.

# Verification

[test_matrix_ui_design](../tests/test_matrix_ui_design.md), rows D-10 through
D-18.
