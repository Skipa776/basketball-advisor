---
type: test_matrix
title: Test Matrix — UI and Design System
description: Required cases for token discipline, contrast in both themes, the draft-board interaction budget, and the accessibility gate.
tags: [tests, ui, design, accessibility, matrix]
source_paths: [src/FantasyBasketball.Api/Components]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [required_gates.md, ../contracts/design_system_contract.md, ../components/web_ui_blazor.md]
status: partial
last_updated: 2026-07-31
owners: [engineering]
---

# Responsibility

Gates R19 and R23. Row `D-14` is the one that decides whether this is usable
during a live draft; rows `D-28`–`D-32` are the ones that decide whether a person
who has never seen the app can get anywhere in it.

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
| `D-17` | Every page rendered with no data | A styled first-run surface — `EmptyState`, `AuthPanel`, or `NotBuiltState`. Pages populated in every state are exempt **by name**, never by omission | ✅ |
| `D-18` | Automated a11y scan, every page, both themes | Zero violations (`accessibility_violations_allowed = 0`) | ✅ |
| `D-22` | Every pair **inside `.on-dark`**, plus the island's own token values | Meets its floor against the island's surfaces, and matches the dark set exactly | ✅ |
| `D-24` | Carousel script and markup | Reduced-motion guard, operable pause, hover and focus suspension, and slides that do not announce themselves (WCAG 2.2.2) | ✅ |
| `D-27` | Every rule applying `--font-display` | Never lands on a numeric selector; `[data-numeric]` stays mono and tabular | ✅ |

`D-22` exists because `.on-dark` renders the same fill in both themes, so nothing
about the ambient theme predicts what is legible inside it. The pair that fails
without it is the light rank teal on the island: **2.66:1** under the hardwood
palette (it was 2.86 before). The row also asserts
the island equals the dark set, because a light set pasted there would satisfy
every contrast pair while rendering a panel indistinguishable from the page.

# The interaction path (`D-28`–`D-32`)

Gates R23. Every row here failed at least once on the shipped app, which is why
each is a row and not a guideline.

| ID | Case | Expected | Required |
|---|---|---|---|
| `D-28` | Every `@page` route, enumerated by reflection | Reachable from a link or nav entry on some rendered page. An orphan route fails | ✅ |
| `D-29` | Every marketing CTA, rendered anonymously | Its destination is reachable under the instance's current configuration — a CTA that lands on "unavailable" is a failure, not a redirect | ✅ |
| `D-30` | Every page at **390 CSS px** | No horizontal document overflow. Decorative backdrops may exceed the viewport; content and controls may not | ✅ |
| `D-31` | The app landing page and `site/index.html` | Same product name and wordmark. Two front doors naming two products fails | ✅ |
| `D-32` | Every input whose label names an identifier | Either a `<select>`, or an `aria-describedby` that resolves to text naming where the value comes from | ✅ |
| `D-33` | Every page with no backing capability | Renders `NotBuiltState` naming its requirement or roadmap entry, and renders **no** numeric player or team data. The string "coming soon" fails | ✅ |
| `D-34` | Every binary under `wwwroot/img` | Has an `ASSETS.md` entry with source, licence, author, and retrieval date | ✅ |

`D-28` exists because `/welcome` shipped with no inbound link and a green suite:
the route sweep rendered it, so every test passed, and no user could reach it.
Rendering a route is not reaching it.

`D-29` catches the shape where a page is individually correct and the sequence is
not. The landing CTA and the closed-registration default are each right on their
own; the pair is a dead end on the product's front door.

`D-31` is a string assertion, not a design judgement. It cannot tell whether the
two pages look related — only that they are not visibly describing two different
products, which is the failure that actually shipped.

`D-33` is what makes a designed shell safe to ship. Three surfaces — Trade
Analyzer, Free Agents, Leaderboard — exist as full layouts over capabilities
nobody has built, because the interface could not be evaluated with a third of
its navigation missing. The row makes the honesty mechanical: name the
requirement, render no numbers. Without it, "shell" decays into "fabricated demo"
in one commit by someone who wanted a screenshot to look better.

`D-34` exists because an unattributed binary is a licensing problem that is
invisible until it is expensive. The check is cheap and runs on every commit.

# What no row here asserts

Composition, hierarchy, and rhythm are reviewed by a human against `DESIGN.md`,
per [run_design_process](../tasks/run_design_process.md). No test distinguishes a
first viewport with one dominant element from three of equal weight, and one that
claimed to would be worse than the review it replaced. R23's "worth looking at"
half is deliberately unmechanised — the mechanised half above is what stops the
review from being spent on defects a grep could have found.

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

`dotnet test --filter Api`, inside the full gate. `D-14` and `D-30` need a laid-out
page and run in `scripts/ui-browser-gate.mjs`. Design review
([run_design_process](../tasks/run_design_process.md) step 6) runs after these are
green, not instead of them.
