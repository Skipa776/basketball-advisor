# Design brief

Input for `/design-consultation`, which produces `DESIGN.md`. Written so the
consultation starts from the real problem instead of interviewing you about things
the repository already knows.

**What is already decided and not up for discussion** is in
`context/codebase_okf/contracts/design_system_contract.md` — token vocabulary,
component inventory, the draft-board interaction budget, WCAG 2.2 AA as a gate.
This brief covers what is genuinely open: the aesthetic.

---

## What the product is

A fantasy basketball decision engine. It answers *given my exact league rules, my
roster, the schedule, current NBA roles, and recent basketball context, what
decision gives my team the highest expected value* — and it shows its reasoning
every time.

Self-hosted, single instance, small number of accounts. Not a SaaS, not a
marketing site, no landing page, no signup funnel. Someone runs this for
themselves and maybe a couple of league-mates.

## Who uses it

One person who takes their fantasy league seriously enough to run a container for
it. They already use a spreadsheet and one of the big fantasy sites. They are not
a novice; they do not need concepts explained; they will notice immediately if a
number looks wrong.

They are the operator and the user, which is unusual and matters: they will see
the empty first-run state, the import-progress screens, and the failure states as
often as they see the happy path.

## The four surfaces that matter, hardest first

### 1. Draft Assistant — the one that decides the design

Ninety seconds per pick. The user is watching a real draft room somewhere else and
this app in another window. Fifty rows of players scanned under time pressure,
re-ranking after every pick.

Everything hard about this design lives here: density without illegibility,
keyboard-first input, and a board that must not move under the cursor when it
re-ranks. Design the system on this screen. A system designed on the Dashboard
and then applied here tends to fall apart on contact.

### 2. Players — projection decomposition

The product's central claim rendered: observed → baseline → context adjustment →
final, with the evidence and the risks beside it. If a user cannot see at a glance
*why* a projection moved, the app is just another number.

### 3. Context Review — human-in-the-loop

A queue of proposed role and news events, each with a verbatim source quote, that
a human accepts, edits, or rejects. Reads more like a review tool than a dashboard.

### 4. Data Sources — the honest one

Which sources are stale, which import failed, how much of the scrape budget is
consumed, how far the long-running box-score import has got (it takes hours). This
screen is where the app admits what it does not know, and it should feel like a
first-class part of the product rather than a settings afterthought.

## The tone problem, stated plainly

This tool tells you a projection is uncertain, that a breakout is shooting-driven
and will probably regress, that it cannot judge whether a trade is fair to the
other manager. **An aesthetic that reads as marketing undermines the only thing
the product is actually claiming.**

It should feel like an instrument: trustworthy, legible, calm, and comfortable
displaying a number next to the words *low confidence*. Not austere for its own
sake — someone uses this for fun, on a Tuesday night — but confident rather than
enthusiastic.

## Questions worth your judgment

1. **How dense before it stops being readable?** The draft board wants maximum rows
   on screen; a scannable table wants air. Where is the line, and does it differ
   between the board and the Players page?
2. **What does it look like at 11pm?** Dark mode is required, not a variant — drafts
   and waiver decisions happen at night. Design it as a first-class theme rather
   than an inversion of the light one.
3. **How do uncertainty and risk look?** Four confidence levels, supporting versus
   risk evidence, stale data, unverified context. These must be visually distinct
   without colour alone (accessibility) and without alarm (they are normal states,
   not errors).
4. **What is the first-run experience?** No players, no league, no imports — the
   actual first screen. It needs to feel like a beginning rather than a broken app.
5. **Numbers.** Tabular figures, alignment, how many decimals to show, and where
   precision should be deliberately withheld because the model does not have it.

## Constraints the aesthetic must live inside

- **Blazor Server**, scoped component CSS, one token file. No CSS framework, no
  component library.
- **Light and dark**, both first-class, with a persisted manual override.
- **WCAG 2.2 AA is a gate** (`accessibility_violations_allowed = 0`): 4.5:1 body
  contrast in both themes, visible focus, 24×24px targets, keyboard-operable
  everywhere.
- **Tables are the primary layout** for player data. Cards are for single-subject
  summaries.
- **Charts** are limited and specific: category profile, trend sparkline,
  projection decomposition, back-test calibration. Series colours come from the
  design tokens.
- Whatever is chosen becomes **token values** in one file. If a decision cannot be
  expressed as a token, it is probably a structural change and belongs in the
  contract instead.

## Deliberately out of scope

No landing page, no onboarding tour, no marketing copy, no illustration system, no
mascot, no logo work beyond something serviceable in a browser tab.

## Process

`context/codebase_okf/tasks/run_design_process.md` has the full order: this brief →
`/design-consultation` → `/design-shotgun` on the Draft Assistant →
`/plan-design-review` → an agent ports the result into Blazor components (E05).
