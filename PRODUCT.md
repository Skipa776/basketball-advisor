# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

**Primary — the operator-user.** One person who takes their fantasy league
seriously enough to run a container for it. They already use a spreadsheet and one
of the big fantasy sites. They are not a novice, they do not need concepts
explained, and they will notice immediately if a number looks wrong. They are the
operator *and* the user, which is unusual and load-bearing: they see the empty
first-run state, the import-progress screens, and the failure states as often as
they see the happy path.

**Secondary — friends and league-mates.** A small number of people given accounts
on the operator's instance. They are users, not operators: they arrive without the
operator's context, they do not own Data Sources, and they have no reason to know
what a scrape budget is.

**The situations.** During a live draft — roughly ninety seconds per pick, this app
in one window and the real draft room in another. And mid-season, for waivers,
streaming, and trades, frequently at night.

## Product Purpose

Answer a more useful question than *who has the highest average fantasy score*:
given my exact league rules, roster, opponents, available players, schedule,
current NBA roles, and recent basketball context, what decision gives my team the
highest expected value?

Success is a decision the user can act on inside a pick clock and still audit
afterwards — every recommendation carries structured evidence, a confidence level,
and a projection decomposed into its statistical baseline and its contextual
adjustment.

## Positioning

Four mechanisms a neighbouring product could not truthfully copy:

- **Two parallel evidence systems.** Quantitative production and structured context
  events, kept separate. Context never overwrites the baseline; baseline,
  adjustment, and final are three distinct persisted records and three visible
  numbers.
- **The statistics make the recommendation.** A language model may *propose* a
  context event from an article for a human to review. It never makes a
  recommendation and has no path to one.
- **No provider is load-bearing.** The domain never learns whether data came from
  an API, an HTML scrape, a CSV, or a human typing it in. A dead integration
  lowers confidence and surfaces staleness; it does not break the app.
- **Your league's rules, not a provider's defaults.** No ESPN/Yahoo default is ever
  assumed as a fallback value.

## Operating Context

Self-hosted, single instance, a handful of accounts. Docker Compose plus
PostgreSQL 17; no committed `appsettings.json`, so every setting comes from the
environment. `scripts/run.sh` is the entry point.

The first account registered claims the instance as owner and closes registration;
open registration is a config choice, closed by default. **How league-mates get
accounts is an open decision** — owner-provisioned or registration reopened.

Data arrives by import, never by assumption: balldontlie for players/teams/schedule
(documented API, key required), Basketball-Reference for season statistics (HTML
scrape, allowlisted season pages only), FantasyPros for ADP (scrape, CSV, or manual),
Yahoo OAuth planned, ESPN by CSV and manual only. The self-imposed scrape ceiling is
6 requests per minute. `*/gamelog/` is robots-disallowed, so per-game data comes from
`/boxscores/` via a background importer that takes **hours per season** — a fact the
UI has to be honest about rather than hide behind a spinner.

## Capabilities and Constraints

**Scope is closed.** `PROJECT_REQUIREMENTS.md` R1–R23 is the whole product. R1–R10
are the MVP; R11–R23 are the post-MVP epics in `docs/epics/`. Nothing outside R1–R23
is in scope.

**The current objective is R23 — interface quality and the interaction path**
(epic E13, added 2026-07-31). It adds no capability: it makes the path through
what already ships finishable, coherent, and worth looking at. Prefer an
interaction fix over a functional one while it is the objective.

**Current state** (canonical: `context/codebase_okf/assumptions.md`). Domain, stat
vocabulary, league configuration, scoring engines, persistence, player identity,
ingestion, and the balldontlie and Basketball-Reference adapters are implemented;
the design system, landing page, auth, and first-run setup ship; projections, draft
board, context engine, trends, categories, streaming, trades, LLM proposal,
back-testing, and distribution are specified and being built epic by epic.

**Technical constraints on any interface work:**

- Blazor Server, scoped component CSS, one token file. No CSS framework, no
  component library.
- Light and dark are both first-class themes with a persisted manual override —
  dark is not an inversion.
- WCAG 2.2 AA is a gate: `accessibility_violations_allowed = 0`.
- Tables are the primary layout for player data; cards are for single-subject
  summaries.
- Charts are limited and specific: category profile, trend sparkline, projection
  decomposition, back-test calibration. Series colours come from tokens.
- Sample content is opt-in behind `Demo:Enabled` and always labelled fictional.

**Terminology that must stay exact:** baseline / context adjustment / final
projection; context event (`Proposed` → human → `Verified`); the four confidence
levels; supporting vs risk evidence; ADP; tier break; punt; *usable* games (not
scheduled games); risers and fallers; opportunity vs efficiency.

**Open decisions:** whether the Fastbreak rename propagates to the solution,
namespaces, and repository directory; how league-mate accounts are provisioned.

## Brand Commitments

**The product is called Fastbreak** (confirmed 2026-07-31). The app's own surfaces
carry it: header, footer, sidebar, auth panel, page titles, favicon. Still reading
`FantasyBasketball` or "Fantasy Basketball Decision Engine": `README.md`, the
GitHub Pages project page at `site/index.html`, the solution file, namespaces, and
the repository directory. Whether the rename reaches the code is undecided — the
*name* is decided, the *rename scope* is not.

- **Voice: it reports, it does not sell.** The product tells you a projection is
  uncertain, that a breakout is shooting-driven and will regress, and that it
  cannot judge whether a trade is fair to the other manager. It is comfortable
  printing "low confidence" directly beside a number without that reading as an
  error. Confident rather than enthusiastic.
- **Imagery is licensed or drawn — never appropriated.** *Amended 2026-07-31:*
  this previously read "drawn, never photographed", which ruled out binary assets
  entirely. Licensed raster artwork is now permitted on the marketing and auth
  surfaces, under conditions that are the actual commitment:
  - **Player likeness is permitted where the photographer licensed it**
    (decided 2026-07-31, reversing the blanket ban above it). A portrait ships
    only when the photographer released it under a licence allowing reuse and
    that licence is recorded in `ASSETS.md` — Wikimedia Commons contributors
    publishing under CC BY-SA are the intended route.
  - **Commercial stock remains unusable**, and this is not the same question.
    Getty, iStock and similar license their catalogue; the photographer's
    copyright is independent of the subject's likeness, so neither a small
    audience nor private use makes copying lawful. The repository is public.
    The two player photographs in `inspiration-resources/` are mood reference
    and cannot ship.
  - Every shipped binary carries source, licence, author, and retrieval date in
    `ASSETS.md`. *Check: row `D-34`.*
  - The SVG `CourtBackdrop` stays as the fallback, so a missing or blocked asset
    degrades to a designed surface rather than a broken one.
  - The instrument stays image-free. Photography is for the surfaces read once.
- **Third-party names are import formats, not partners.** ESPN, Yahoo, and Sleeper
  appear in plain type with a no-affiliation line that ships inside the component,
  so a page cannot render the names without it. No logos, no sponsors row.
- One self-hosted display face (Anton, SIL OFL). No third-party font host, ever.
- No mascot, no illustration system beyond the court backdrop. The favicon, the
  wordmark, and one display face are the whole brand.
- MIT licensed.

## Evidence on Hand

**Available to use:** real screenshots of the running app; labelled-fictional demo
data behind `Demo:Enabled` (rows `A-26`, `D-26`).

**Absent — must not be fabricated:**

- **No measured accuracy exists.** R18 back-testing has not run, so there are no
  MAE, RMSE, rank-correlation, hit-rate, or calibration figures to cite, and the
  draft weights are still documented guesses rather than fitted values.
- No users, testimonials, case studies, press, benchmarks, or pricing.
- The container image, one-command quickstart, and seeded demo data arrive with
  epic E12 and do not exist yet.
- Terms-of-use compliance for each scraped host is the operator's judgment; the
  product does not claim it.

## Product Principles

1. **Report, don't sell.** An aesthetic or a claim that reads as marketing
   undermines the only thing the product is actually claiming.
2. **The statistics decide; context annotates.** Inferred or scraped context never
   becomes ground truth without a human action.
3. **Degrade loudly, stay useful.** Manual entry always works. A dead source lowers
   confidence and shows its staleness rather than breaking a screen.
4. **Every number is decomposable.** A recommendation with zero evidence items is a
   bug, not a display choice.
5. **Emptiness and failure are first-class screens**, because the operator-user sees
   them as often as the happy path.

## Accessibility & Inclusion

WCAG 2.2 AA as a gate, not an aspiration: `accessibility_violations_allowed = 0`.
4.5:1 body contrast in both themes, visible focus, 24×24px targets, keyboard-operable
everywhere. **The draft board is keyboard-operable end to end.** Risk and confidence
are never conveyed by colour alone.
