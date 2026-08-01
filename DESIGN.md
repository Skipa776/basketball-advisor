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

There are now **two surface classes**, and they are deliberately different:

- **Marketing and auth** — the landing page, sign in, sign up, first-run setup.
  These are read once, by someone deciding whether to bother. Big display type,
  full-bleed art, the accent used freely.
- **The instrument** — the board, tables, review queues. Read under time
  pressure, repeatedly. Dense, quiet, and the accent is rationed.

Four consequences that drive every value below:

1. **Hardwood is the primary theme.** The first screen anyone sees is the landing
   page, and it is court-line cream over maple. Dark is a night gym in walnut —
   fully supported, equally tested, and not an inversion — but light is the one
   designed first.

   The wood is load-bearing and it is also a trap: a mid-tone maple cannot carry
   body text at 4.5:1. So the text-bearing surfaces stay light and warm, and the
   saturated maple lives in `--color-surface-wood`, which is **decorative and
   carries nothing**. That split is what lets the shell read as a court without
   putting a single label on a surface that cannot hold one.
2. **Numbers are the interface.** Tabular figures, right-aligned, monospace. A column
   of misaligned decimals is unreadable at the speed a draft demands. The display
   face never touches a number (row `D-27`).
3. **Decoration is subtractive *on the instrument*.** Hairline rules instead of
   boxes. The accent is spent on a single thing there — the recommended pick — and
   row `D-23` enforces that nothing else on the board may use it. Outside the
   board it is the brand colour and may be used freely.
4. **Nothing is measured against the page it is near.** A token is measured against
   the fill it actually sits on. This is what produces both the two border tokens
   and the always-dark island below.

---

## Colour

Every pair below was verified against WCAG 2.2 AA before being written down.
Ratios are computed, not estimated.

Ratios below are quoted **bg · surface · raised**.

### Light (primary)

| Token | Value | Role | Contrast |
|---|---|---|---|
| `--color-bg` | `#F7F1E7` | Page — court-line cream over maple | — |
| `--color-surface` | `#FFFCF7` | Panels, table body | — |
| `--color-surface-raised` | `#EDE3D2` | Hover, selected row, popovers | — |
| `--color-rule` | `#DFD3BE` | **Decorative** separators only | exempt — see note |
| `--color-border-strong` | `#756A59` | **Interactive** boundaries: inputs, buttons | **4.72 · 5.18 · 4.17** |
| `--color-text` | `#1A1512` | Body — deep brown-black, not neutral | **16.11 · 17.69 · 14.24** |
| `--color-text-muted` | `#5A5147` | Secondary, labels, units | **6.92 · 7.59 · 6.11** |
| `--color-accent` | `#C85A10` | Fills, CTAs, display type | **3.79 · 4.17 · 3.35** — UI and large text only |
| `--color-accent-ink` | `#A94209` | Orange **as small text** | **5.39 · 5.91 · 4.76** |
| `--color-accent-contrast` | `#0A0807` | Label **on** an orange fill | **4.69** on accent |
| `--color-rank` | `#1A6670` | Carousel and list rank numbers | **5.88 · 6.46 · 5.20** |
| `--color-positive` | `#1A653F` | Supporting evidence, gains | **6.28 · 6.89 · 5.55** |
| `--color-negative` | `#A32E1C` | Risk evidence, losses | **6.30 · 6.92 · 5.57** |
| `--color-caution` | `#75540A` | Stale data, unverified context | **6.17 · 6.77 · 5.45** |
| `--color-surface-wood` | `#C9A277` | Maple field behind the shell | exempt — **decorative, carries nothing** |
| `--color-court-line` | `#E8D9BE` | Court geometry drawn on the wood | exempt — decorative |

### Dark

| Token | Value | Role | Contrast |
|---|---|---|---|
| `--color-bg` | `#16110D` | Page — night gym, walnut | — |
| `--color-surface` | `#1E1812` | Panels, table body | — |
| `--color-surface-raised` | `#2A221A` | Hover, selected row, popovers | — |
| `--color-rule` | `#332A21` | Decorative separators only | exempt |
| `--color-border-strong` | `#7C7062` | Interactive boundaries | **3.88 · 3.64 · 3.24** |
| `--color-text` | `#F4EEE3` | Body | **16.24 · 15.22 · 13.55** |
| `--color-text-muted` | `#A99E8E` | Secondary, labels, units | **7.11 · 6.67 · 5.94** |
| `--color-accent` | `#E5761F` | Fills, CTAs, display type | **6.20 · 5.81 · 5.17** |
| `--color-accent-ink` | `#F2954C` | Orange as small text | **8.19 · 7.68 · 6.84** |
| `--color-accent-contrast` | `#0A0807` | Label on an orange fill | **6.61** on accent |
| `--color-rank` | `#5FC4CE` | Rank numbers | **9.17 · 8.60 · 7.65** |
| `--color-positive` | `#63C08F` | Supporting evidence | **8.45 · 7.92 · 7.05** |
| `--color-negative` | `#EA8771` | Risk evidence | **7.32 · 6.87 · 6.11** |
| `--color-caution` | `#DBA742` | Stale, unverified | **8.59 · 8.05 · 7.17** |
| `--color-surface-wood` | `#3A2C1F` | Maple field behind the shell | exempt — decorative |
| `--color-court-line` | `#4A3826` | Court geometry | exempt — decorative |

### Why there are two orange tokens

One orange cannot carry small text on both a cream page and a walnut panel, and
the warmer surfaces make this *worse* than it was on off-white — a maple-tinted
background sits closer in luminance to the orange than a neutral one did.
Measured: the dark theme's `#E5761F` scores **2.69** on the light page, failing
even the 3:1 fill floor, while the light theme's `#A94209` drops to **2.90** on
the dark panel. Neither can cross. So:

- **`--color-accent`** — fills, CTAs, and display type ≥24px. Held to 3:1.
  `#C85A10` is the boldest orange clearing that on *all three* light surfaces.
  `#D05E11` still passes at **3.12** on raised; `#D46412` fails there at **2.93**,
  which is where the ceiling actually is.
- **`--color-accent-ink`** — the darker orange, for the cases where orange has to
  be small text. Held to 4.5:1.

**Labels on an orange fill are near-black, never white**: `#0A0807` scores 4.69
against the fill, white only 4.26. That margin narrowed with the darker accent —
it was 5.03 against 3.58 on the old palette — so the label token moved from
`#16161A` to `#0A0807` to keep it. This is the pair with the least headroom in
the whole system; changing the light accent means re-checking it first.

### The always-dark island

`.on-dark` renders the dark token set regardless of the ambient theme. The player
panel uses it, which is what makes it read as a slab cut out of the page.

It exists because a token is measured against the fill it sits on: the light rank
teal on that panel is **2.66:1** and fails, and the light body text lands at
**1.03:1** — very nearly invisible. Inside the island every pair is re-checked
against the island's own surfaces — cream text **15.22**, accent **5.81**, rank
**8.60**, positive **7.92**, negative **6.87**. Row `D-22` also asserts the island
matches the dark set exactly, because a light set copied there would pass the
pairs while rendering a panel indistinguishable from the page.

### Why there are two border tokens

WCAG 1.4.11 requires 3:1 for the boundaries of **interactive** components, not for
decorative dividers. Forcing a table's row rules to 3:1 produces a grid of visible
lines and destroys the hairline aesthetic; leaving input borders below 3:1 is an
accessibility failure. One token could not serve both, so:

- **`--color-rule`** — row separators, section dividers. Purely decorative, exempt
  by construction. Rows must remain distinguishable without it.
- **`--color-border-strong`** — anything the user can focus, type into, or click.
  **Never below 3:1 against any surface it can sit on** — including
  `--color-surface-raised`, which is the one that gets missed: a bordered
  control whose own fill is the raised surface has its boundary measured
  against that fill, not against the page.

Using `--color-rule` on an interactive boundary is a bug.

### The accent rule

**On the draft board, `--color-accent` marks the recommended pick and nothing
else.** Not headings, not links in tables, not the active nav item. Its entire job
there is to be the one thing your eye lands on with four minutes left on the
clock. Row `D-23` scans the board's stylesheets and fails on any other selector.

**Everywhere else it is simply the brand colour** — the hero, CTAs, the nav
wordmark, card edges. That split is the whole reason the board still works: the
accent is loud on surfaces you read once and rationed on the surface you read
under pressure.

`--color-positive` / `--color-negative` are for **evidence polarity only**, and
always accompanied by an icon and a text label — never carrying the meaning alone
(WCAG 1.4.1, and required by row D-12).

---

## Type

```
--font-sans:    -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif
--font-mono:    ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, monospace
--font-display: "Anton", "Arial Narrow", var(--font-sans)
```

Body and numbers use system stacks. **One display face is self-hosted** — Anton,
SIL OFL, latin subset, 12KB, served from `wwwroot/fonts/` with its licence beside
it.

This narrows the old rule rather than reversing it. The reason for that rule was
that "a self-hosted tool should not make a network request to render, and the CSP
would block it anyway" — serving the font ourselves keeps both true. **No
third-party font host, ever.**

The display face is for the wordmark, hero headline, and section headings only.
**It never renders a number**, because Anton is condensed and non-tabular and a
column of misaligned decimals is the one thing the board cannot afford. Row `D-27`
enforces it.

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
| `--text-2xl` | 32px / 1.25 | Section headings on marketing surfaces |
| `--text-3xl` | 44px / 1.25 | Hero accent line |
| `--text-4xl` | 60px / 1.25 | Hero headline below 56rem |
| `--text-hero` | 88px / 0.9 | The hero headline. One place, one size |

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
--ease-out: cubic-bezier(0.2, 0, 0, 1)
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
- **The row under the cursor never moves.** Fixed row height, and after a re-rank
  the scroll position is corrected so every surviving row keeps the same offset
  inside the viewport. Rows do **not** animate into their new rank: a row that
  translates fights the correction, and the correction is what the requirement
  actually asks for. The consequence is that this board has no motion for
  `prefers-reduced-motion` to reduce — re-rank is already instant for everyone.

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

## The public surfaces

A landing page, a sign-in page, and a first-run setup flow now exist. This
reverses an earlier decision in this file, deliberately: the tool had no surface
that explained itself to someone who had not already been sold on it.

- **Landing** — hero, risers/fallers carousel, the always-dark rank panel,
  coverage, footer. Renders at `/` for anyone signed out.
- **Auth** — full-bleed court art, one centred card. No site chrome; there is
  nothing to navigate to yet.
- **Imagery** — licensed or drawn, never appropriated. *Amended 2026-07-31; this
  read "drawn, never photographed", which ruled out binary assets outright.*
  Licensed raster artwork is now permitted **here and on auth only**, and every
  shipped binary carries source, licence, author, and retrieval date in
  `ASSETS.md` (row `D-34`). `CourtBackdrop` — SVG court geometry and
  non-identifiable silhouettes — stays as the fallback, so a missing or
  CSP-blocked asset degrades to a designed surface rather than a broken one.
  **No NBA, team, or identifiable player likeness in any medium.** The instrument
  stays image-free: photography belongs to the surfaces read once, not to the one
  read under a clock.
- **Third-party names** — ESPN, Yahoo and Sleeper appear as *import formats*,
  in plain type, with a no-affiliation line that ships inside the component so a
  page cannot render the names without it. No logos, no sponsors row.
- **Motion** — the carousel auto-advances, has a visible pause, stops on hover
  and focus, and does not move at all under `prefers-reduced-motion` (row `D-24`,
  WCAG 2.2.2). It is the only auto-motion in the product.

Sample content on these surfaces is **opt-in via `Demo:Enabled` and always
labelled fictional** (rows `A-26`, `D-26`). With the flag off the landing page
shows its own empty state rather than invented numbers.

## Out of scope

No mascot, no illustration system beyond the court backdrop, no marketing
animation. The favicon, the wordmark, and one display face are the whole brand.

---

## Changing this file

Editing `DESIGN.md` changes token **values** only. If a change needs a new token, a
new component, or a different interaction budget, that is a
`design_system_contract.md` change and goes there first.

**Any colour change re-runs the contrast check in both themes.** The ratios in this
file are load-bearing, and `accessibility_violations_allowed = 0` is a gate.
