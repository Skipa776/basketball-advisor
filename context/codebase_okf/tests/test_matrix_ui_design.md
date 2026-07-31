---
type: test_matrix
title: Test Matrix — UI and Design System
description: Required cases for token discipline, contrast in both themes, the draft-board interaction budget, and the accessibility gate.
tags: [tests, ui, design, accessibility, matrix]
source_paths: [src/FantasyBasketball.Api/Components]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [required_gates.md, ../contracts/design_system_contract.md]
status: implemented
last_updated: 2026-07-30
owners: [engineering]
---

# Responsibility

Gates R19. Row `D-14` is the one that decides whether this is usable during a live
draft.

# Token discipline (`D-10`, `D-19`, `D-20`, `D-23`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-10` | Scan `.razor` and `.razor.css` | No hex colour, raw `px` spacing, or `ms` duration outside the token file | ✅ |
| `D-19` | `Components/Design` types | No service or repository dependency — components are presentational | ✅ |
| `D-20` | Every page | Composed from inventory components; defines no page-level styles. `PageHeader` **or** `AuthPanel` supplies the heading | ✅ |
| `D-23` | Every rule using `--color-accent` in a board stylesheet | Every comma-separated selector part names the recommendation. Orange is free elsewhere and rationed here | ✅ |

`D-23` is the executable half of the accent rule. The accent became the brand
colour when the marketing surfaces landed, and a brand colour sprayed across the
board would cost the recommended row the only job it has: being the one thing the
eye finds with four minutes on the clock. Selector parts are checked individually,
so `.anything, .recommendation` cannot smuggle a rule past it.

# Public surfaces (`D-25`, `D-26`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-25` | `GET /` with no session | `200` with the landing content, and **none** of the dashboard's owned-data sections | ✅ |
| `D-26` | Landing page with the demo flag off, then on | Off: no badge and none of the invented players. On: the content **and** its fictional label | ✅ |

`D-25` asserts the absence of the owned sections rather than the presence of a
guard, because the page's own catch would swallow a `CurrentUserId` throw and
serve the same `200` either way. What a signed-out visitor can see is the thing
worth pinning.

# Accessibility (`D-11`, `D-12`, `D-15`–`D-18`, `D-22`, `D-24`, `D-27`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-11` | Every foreground token × every surface token, **both themes** | 4.5:1 body, 3:1 UI boundaries; **and** no colour token exists that no pair covers and no exemption names | ✅ |
| `D-12` | Every evidence item | Renders an icon **and** a text label — never colour alone | ✅ |
| `D-15` | Board row stylesheet and the scroll-anchor script | No row transitions a geometry property and the correction never animates — re-rank is instant by construction, so there is no motion left to reduce | ✅ |
| `D-16` | Live regions on the draft board | Announce the pick made and a top-recommendation change; **not** every re-ranked row | ✅ |
| `D-17` | Every page rendered with no data | A styled first-run surface — `EmptyState` or `AuthPanel`. Pages populated in every state are exempt **by name**, never by omission | ✅ |
| `D-18` | Automated a11y scan, every page, both themes | Zero violations (`accessibility_violations_allowed = 0`) | ✅ |
| `D-22` | Every pair **inside `.on-dark`**, plus the island's own token values | Meets its floor against the island's surfaces, and matches the dark set exactly | ✅ |
| `D-24` | Carousel script and markup | Reduced-motion guard, operable pause, hover and focus suspension, and slides that do not announce themselves (WCAG 2.2.2) | ✅ |
| `D-27` | Every rule applying `--font-display` | Never lands on a numeric selector; `[data-numeric]` stays mono and tabular | ✅ |

`D-22` exists because `.on-dark` renders the same fill in both themes, so nothing
about the ambient theme predicts what is legible inside it. The pair that fails
without it is the light rank teal on the island: **2.86:1**. The row also asserts
the island equals the dark set, because a light set pasted there would satisfy
every contrast pair while rendering a panel indistinguishable from the page.

# Draft-board interaction budget (`D-13`, `D-14`, `D-21`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-13` | Enter a pick with the keyboard only | ≤ 3 keystrokes to commit; focus returns to the search field | ✅ |
| `D-14` | **Record row offsets before and after a pick** | No reflow above the current scroll position — the board does not shift under the cursor | ✅ |
| `D-21` | Render the full player pool | Bounded DOM node count — the table is virtualized | ✅ |

`D-14` exists because a board that reflows the instant a rank changes will cause a
misclick on the wrong player during a live draft. It is the highest-consequence UI
bug available in this product and it is invisible to an accessibility scanner.

# What the D-18 scan is

Not axe-core. It is a rule set implemented directly against the server-rendered
DOM (AngleSharp), so the whole suite stays offline and dependency-free. It
asserts: document language; exactly one `main` and one `h1`; no duplicate `id`;
every `aria-describedby` / `aria-labelledby` / `aria-controls` resolves to a real
element; `alt` on every image; an accessible label on every form control; an
accessible name on every button and link; a caption and header cells on every
table; a label on every `nav`; and the live-region count required for that page.

What it structurally cannot see, because it never lays the page out: colour
contrast (that is `D-11`, computed from the tokens instead), focus visibility,
and any state that only exists after Blazor becomes interactive. Read a green
`D-18` as "the markup contract holds", not as "axe found nothing".

# What the scan does not cover

`D-18` catches roughly the mechanical half of accessibility. `D-12`, `D-14`, and
`D-16` exist because the other half — announcing the *right* things, and not moving
the target — is the half that decides whether the app works under time pressure. A
green scan is the floor, not the bar.

# Verification

`dotnet test --filter Api`, inside the full gate. Design review
([run_design_process](../tasks/run_design_process.md) step 6) runs after these are
green, not instead of them.
