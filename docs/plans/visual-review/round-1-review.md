> **Archived: owner rejected this appearance. See [round 2](README.md).**

# Fastbreak visual review handover

**Ready for owner review · 18 September 2026 · production migration not started**

Open the [visual review gallery](round-1-gallery.html), then the [interactive Arena Glass prototype](prototype.html). Both work directly from this local folder without a build, account, or server. The gallery's proposed-design screenshots use the existing ignored inspiration folder; keep it beside the repository. The prototype displays a fallback if its two local image references are absent.

The [completed migration plan](../react-liquid-glass-migration.md) covers feasibility, current-versus-React differences, all 14 routes, API gaps, architecture, scope, phases, acceptance criteria, cost, rollback, and owner decisions. The [reference dossier](../react-liquid-glass-references.md) documents Apple, Linear, Nike, NBA, Raycast, and the supplied visual references, including what to borrow and what to avoid.

## Review in five minutes

1. **Landing:** inspect basketball composition, headline scale, orange accent, and the amount of glass. Switch between Light and Dark. The LeBron image is a local mood reference, not a cleared release asset.
2. **Sign in:** compare the Curry portrait treatment and solid form surface. Use fictional details only; this form sends nothing and clears the preview password.
3. **Draft:** type `Jules`, use the arrow keys and Enter, then Undo. Evaluate the dense opaque table, recommendation hierarchy, and supporting/risk evidence. These are fixture interactions, not a connected draft or a React performance demonstration.
4. **Player detail:** inspect the baseline/context/final grouping and visible uncertainty. No NBA photo is associated with the fictional player.
5. Repeat at phone width and toggle **Solid mode**. Compare the current-app captures in the gallery. Review brightness, readability, spacing, and whether the basketball identity feels strong enough.

## Decisions for your review

The plan now has concrete recommendations. These questions remain owner choices; silence has not been treated as approval.

| Question | Recommended choice |
|---|---|
| Does the design feel like the right balance of Apple and basketball? | Arena Glass: restrained material, bold editorial landing, precise workspace |
| More visible glass or stronger sports typography? | Keep subtle glass and system sans; add stronger public-page treatment only if requested |
| Where should photography appear? | Landing and sign-in only; use cleared replacements if these references cannot be licensed |
| Which capabilities ship in the migration? | Existing capabilities across all routes; keep honest unbuilt states; no new engines |
| What comes first? | Prove real draft interaction parity and responsiveness before porting the remaining pages |
| Is JavaScript-dependent public/auth UI acceptable? | Final React SPA on the existing ASP.NET origin; if initial HTML/no-JavaScript sign-in is required, revise the rendering plan first |

Useful feedback format: **screen + theme + viewport + what feels wrong + desired direction**. For example: “Landing, dark, desktop: glass is too quiet; make navigation more visibly translucent while keeping the draft table solid.”

## What Playwright checked

Executed with **Playwright 1.58.2 / Chromium**. Machine-readable results: [prototype](prototype-report.json), [existing app](current/report.json), and [gallery](round-1-gallery-report.json).

### Proposed design

- 16 full-page captures: four screens × desktop/phone × light/dark.
- 12 checks passed: preview pick/undo/focus, no-result search/Escape, skip link, navigation/Back/focus, solid mode, theme naming, non-submitting sign-in, reduced motion, increased contrast, 320-pixel reflow, capture integrity, and runtime errors.
- No horizontal page overflow at 390 or 320 pixels; one visible main landmark; visible inputs labelled; referenced photos loaded; no browser runtime errors.
- Corrected a small-screen league-control overflow and a skip-link route issue during review. Photo composition now retains the ball and basket rather than cropping the action away.
- Reviewed representative rendered desktop and phone captures. Desktop draft rows retain the 28-pixel concept height; this does not prove production virtualization, the existing ranking budget, or semantic parity.

### Existing Blazor application

- 54 captures: 12 signed-in routes × desktop/phone × light/dark, plus landing/login/closed-registration at phone width in both themes.
- Real UI registration, league creation, draft start, type–arrow–Enter pick, undo/focus, context creation/verification, logout, and sign-in exercised against fictional records.
- Seeded 60 fictional players and projections. Real Identity cookies and application services were used; provider HTTP was blocked, hosted workers disabled, and PostgreSQL ran in a disposable container. No developer database or private league was used.
- The captured set covers all 14 route paths across relevant signed-in/out states, not every state combination on every route. Player detail selection, imports, context-impact overrides, multiple-account isolation, offline recovery, and full performance protocols remain migration acceptance work.

## Findings to carry into implementation

| Finding | Evidence | Proposed action / priority |
|---|---|---|
| Closed-registration landing still advertises “Create account” | Both closed-registration CTA checks fail; [light landing](current/phone-light-landing-anonymous.png) and [closed registration](current/phone-light-account-register-anonymous.png) | Gate the CTA with the server's registration-availability state; P1 public/auth parity |
| Populated Context Review overflows the phone viewport | Both phone themes in `current/report.json`; review table extends beyond the page | Use a mobile review-card layout or labelled internal table scroller; keep Verify/Reject and provenance accessible; P1 responsive parity |
| Long player names lose useful identity in the desktop draft | [Current draft](current/desktop-light-draft.png): repeated fictional names truncate despite available screen width | Give the ranked board the primary width and make the pick control compact; test realistic long names and explicit accessible names; P2 layout |
| Context entry asks users to copy raw identifiers | [Current Context Review](current/desktop-light-context-review.png), source-backed form labels/help | Add player search and event selection backed by existing contracts, retaining identifiers in details; P2 workflow refinement |
| Existing public page and workspace need stronger hierarchy | Current versus proposal gallery; visual judgment, not an automated defect | Use the proposed type/spacing system and public photography without adding decoration inside data rows |

A server log also recorded an EF disposed-context exception during the rapid-navigation/fixture-lifecycle run. No corresponding browser runtime error or failed final workflow was observed; reproduce and isolate it before classifying it as a production defect. The review host and disposable database container were stopped and removed after capture.

The first two are browser-confirmed product defects. The remaining entries are review judgments grounded in the rendered UI. They can be addressed in Blazor too; they are not evidence that React is inherently necessary.

## Verification boundaries

This is a **static HTML/CSS/JavaScript design prototype**, not the React migration. Its numbers and evidence are explicitly fictional. The recommendation example stays illustrative while preview picks change; it does not rerank. The authentication preview does not authenticate. Only four representative screens are designed interactively; the plan specifies the rest.

This audit used Chromium, not Firefox, WebKit, real iOS Safari, assistive technology, or a full automated accessibility engine. Labels, focus, motion, contrast preference, and layout checks do not constitute WCAG conformance. Composited glass contrast, production bundles, realistic latency, server rankings, and asset rights remain explicit acceptance work.

No app code, engine, dependency pin, canonical contract, or OKF implementation status changed. Local photo-bearing screenshots stay under `inspiration-resources/react-review/`, already ignored by Git. Current-app screenshots contain only the disposable fictional fixture.

## Repository gate

The temporary review host built successfully with zero warnings/errors. The full `scripts/gate.sh` run stopped at locked restore with **NU1903** for the pre-existing `SSH.NET` **2025.1.0** dependency in the integration-test dependency graph, citing `GHSA-mggc-4xg6-vcxf` and `GHSA-q939-rpr3-3284`. Subsequent full-gate stages did not run in that invocation. No vulnerability check was disabled and no dependency changed to force the gate green.

The planning artifacts remain uncommitted because the repository requires a green gate at commit boundaries. This does not prevent local visual review. Address the dependency through the normal approved pin/update process before the implementation commit.
