# DESIGN.md

The aesthetic source of truth. Canonical for **how it looks**;
`context/codebase_okf/contracts/design_system_contract.md` is canonical for
**what exists** — token names, component inventory, the interaction budget, the
accessibility floor. This file supplies values for those tokens and nothing else.

Direction chosen from `DESIGN_BRIEF.md`: **Instrument**, at **compact** density.

---

## The idea

An instrument, not an interface. This tool tells you a projection is uncertain,
that a breakout is shooting-driven and will probably regress, and that it cannot
judge whether a trade is fair to the other manager. It has to look like something
that reports rather than something that sells — **comfortable putting the words
"low confidence" directly beside a number, without that reading as an error.**

Three consequences that drive every value below:

1. **Dark is the primary theme.** Drafts and waiver decisions happen at night. Light
   is fully supported and equally tested, but dark is the one designed first.
2. **Numbers are the interface.** Tabular figures, right-aligned, monospace. A column
   of misaligned decimals is unreadable at the speed a draft demands.
3. **Decoration is subtractive.** Hairline rules instead of boxes; one accent colour,
   spent almost entirely on a single thing — the recommended pick. If the accent is
   everywhere, it says nothing.

---

## Colour

Every pair below was verified against WCAG 2.2 AA before being written down.
Ratios are computed, not estimated.

### Dark (primary)

| Token | Value | Role | Contrast |
|---|---|---|---|
| `--color-bg` | `#0E0F12` | Page | — |
| `--color-surface` | `#16181C` | Panels, table body | — |
| `--color-surface-raised` | `#1E2126` | Hover, selected row, popovers | 1.19:1 on bg |
| `--color-rule` | `#2E333B` | **Decorative** separators only | 1.40:1 — see note |
| `--color-border-strong` | `#606874` | **Interactive** boundaries: inputs, buttons | **3.40:1** on bg |
| `--color-text` | `#E6E8EB` | Body | **15.61:1** |
| `--color-text-muted` | `#9BA3AE` | Secondary, labels, units | **7.52:1** |
| `--color-accent` | `#E8A33D` | Recommended pick, focus ring | **8.89:1** |
| `--color-positive` | `#5FBF8E` | Supporting evidence, gains | **8.51:1** |
| `--color-negative` | `#E8836F` | Risk evidence, losses | **7.22:1** |
| `--color-caution` | `#D9A441` | Stale data, unverified context | **8.52:1** |

### Light

| Token | Value | Role | Contrast |
|---|---|---|---|
| `--color-bg` | `#FAFAFA` | Page | — |
| `--color-surface` | `#FFFFFF` | Panels, table body | — |
| `--color-surface-raised` | `#F2F3F5` | Hover, selected row, popovers | 1.06:1 on bg |
| `--color-rule` | `#E3E6EA` | Decorative separators only | 1.25:1 |
| `--color-border-strong` | `#767D87` | Interactive boundaries | **3.98:1** on bg |
| `--color-text` | `#15171B` | Body | **17.19:1** |
| `--color-text-muted` | `#5A626D` | Secondary, labels, units | **5.91:1** |
| `--color-accent` | `#8A5310` | Recommended pick, focus ring | **6.05:1** |
| `--color-positive` | `#1B7A4E` | Supporting evidence | **5.11:1** |
| `--color-negative` | `#A8341F` | Risk evidence | **6.33:1** |
| `--color-caution` | `#7A5806` | Stale, unverified | **6.24:1** |

### Why there are two border tokens

WCAG 1.4.11 requires 3:1 for the boundaries of **interactive** components, not for
decorative dividers. Forcing a table's row rules to 3:1 produces a grid of visible
lines and destroys the hairline aesthetic; leaving input borders below 3:1 is an
accessibility failure. One token could not serve both, so:

- **`--color-rule`** — row separators, section dividers. Purely decorative, exempt
  by construction. Rows must remain distinguishable without it.
- **`--color-border-strong`** — anything the user can focus, type into, or click.
  **Never below 3:1.**

Using `--color-rule` on an interactive boundary is a bug.

### The accent rule

`--color-accent` marks **the recommended pick and nothing else** on the draft board.
Not headings, not links in tables, not the active nav item. Its entire job is to be
the one thing your eye lands on with four minutes left on the clock.

`--color-positive` / `--color-negative` are for **evidence polarity only**, and
always accompanied by an icon and a text label — never carrying the meaning alone
(WCAG 1.4.1, and required by row D-12).

---

## Type

```
--font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif
--font-mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace
```

System stacks. No webfont — a self-hosted tool should not make a network request to
render, and the CSP on any sensible deployment would block it anyway.

**Every number renders in `--font-mono` with `font-variant-numeric: tabular-nums`.**
That includes table cells, the evidence magnitudes, and the clock. Prose renders in
`--font-sans`. This split *is* the Instrument look — more than the palette.

| Token | Size / line-height | Use |
|---|---|---|
| `--text-2xs` | 11px / 1.3 | Column headers, units, timestamps — uppercase, `0.06em` tracking |
| `--text-xs` | 12px / 1.4 | Dense table cells, evidence detail |
| `--text-sm` | 13px / 1.45 | **Board rows — the default** |
| `--text-base` | 15px / 1.55 | Prose, forms, review queue |
| `--text-lg` | 18px / 1.4 | Panel titles |
| `--text-xl` | 24px / 1.25 | Page titles |

Only one weight pair: 400 and 600. No 500, no 700 — a scale with four weights drifts
within a week.

---

## Space, shape, motion

```
--space-1: 2px    --space-4: 8px     --space-7: 24px
--space-2: 4px    --space-5: 12px    --space-8: 32px
--space-3: 6px    --space-6: 16px

--radius-sm: 3px  --radius-md: 5px   --radius-lg: 8px

--motion-fast: 120ms   --motion-base: 200ms
--easing: cubic-bezier(0.2, 0, 0, 1)
```

Small radii deliberately. Rounded corners read as friendly; this should read as
precise.

### Compact density — the numbers that make it real

| Property | Value |
|---|---|
| Board row height | **28px** — fixed, never content-derived |
| Row padding | `--space-2` vertical, `--space-5` horizontal |
| Rows visible at 900px viewport | **~28** |
| Column gap | `--space-6` minimum between numeric columns |

**Vertical padding is what costs rows; horizontal separation is what buys legibility.**
Spend accordingly. Row height is fixed so the board cannot reflow when a value
changes length — which is half of requirement D-14.

Players and Context Review use `--text-base` and taller rows. The board is the only
surface tuned this tight, because it is the only one with a clock.

---

## The draft board

The surface the whole system was designed against.

```
DRAFT · rd 3 · pick 28                      next in 4  ‹ 4:12 ›
────────────────────────────────────────────────────────────────
     PLAYER              POS      VAL      VAR     ADP
▸ 1  J. Morant           PG      38.4     +9.1      31    ◀ take
  2  M. Turner           C       35.2     +5.9      24
  3  D. Sabonis          PF      34.8     +5.5      19
───────────────────────── tier break ───────────────────────────
  4  C. Sexton           SG      29.1     −0.2      44

 WHY MORANT                              conf   MODERATE
 +  min 24.4 → 31.7      +8.6 pts/g
 +  AST fills your weakest slot
 −  FG% 4.1 pts above career
 −  6-game sample
```

Rules this implies:

- **The recommended row is marked by `▸`, the accent colour, and a `take` label.**
  Three signals, only one of which is colour.
- **Tier breaks are a labelled rule**, not a gap. A gap reads as loading.
- **`conf MODERATE` sits beside the recommendation, never hidden.** Confidence is a
  labelled chip in one of four values — never a percentage, never a bar.
- **Risks are always visible.** No disclosure triangle. A recommendation whose risks
  are collapsed is a recommendation without risks.
- **The row under the cursor never moves.** Fixed row height, rows animate position
  over `--motion-base`, and nothing is inserted above the current scroll position
  mid-interaction. Under `prefers-reduced-motion` the move is instant, never absent.

---

## States that ship

The first screen of a fresh self-hosted instance has no players, no league, and no
imports. It is the state a new user actually sees first, and the one most often left
unstyled.

- **Empty** — one line of what this screen will show, one action to get there.
  Never a spinner standing in for emptiness.
- **Loading** — skeleton rows at the real row height, so nothing shifts on arrival.
- **Error** — what failed, in plain words, and what still works. The app degrades
  loudly and stays useful.
- **Stale** — `--color-caution` plus relative age in words (`updated 3h ago`), never
  a timestamp the reader has to subtract from now.
- **Unverified context** — marked inline wherever it moves a number, not only on the
  Context Review page.

---

## Out of scope

No landing page, no onboarding tour, no illustration system, no mascot. The favicon
and a serviceable wordmark are the whole brand.

---

## Changing this file

Editing `DESIGN.md` changes token **values** only. If a change needs a new token, a
new component, or a different interaction budget, that is a
`design_system_contract.md` change and goes there first.

**Any colour change re-runs the contrast check in both themes.** The ratios in this
file are load-bearing, and `accessibility_violations_allowed = 0` is a gate.
