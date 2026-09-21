# Fastbreak — reference-led design study, round 2

**Current review surface:** [open the interactive study](directions.html) · [screenshot gallery](round-2-gallery.html).

The owner rejected the original Arena Glass concept and chose **Maxima Therapy and Sam Walks** as the primary foundations. This round starts with their composition and interaction ideas, then adds basketball identity. SimplyRaffle, Webharu and Konny Amaya remain backups. Apple informs gesture behavior and selective glass controls.

The [migration plan](../react-liquid-glass-migration.md) and [reference dossier](../react-liquid-glass-references.md) now reflect this direction. The original prototype is archived as rejected; its browser results are not evidence for this new study.

## What to open and try

| Direction | Foundation and identity | Try this | Tradeoff |
|---|---|---|---|
| [01 Courtside](directions.html#courtside) | Maxima-inspired large illustrated scene, condensed type and tactile navigation; court geometry and a local athlete photo give it basketball identity | Drag the scene, focus it and press arrow keys, or use the three perspective buttons | Most playful; illustration and palette need your taste judgment |
| [02 Walk the Floor](directions.html#walk) | Sam-inspired spatial journey, landmarks and an index; the local photographs become court-side posters | Move the slider, drag, hold to explore, or jump with Scene index | Most exploratory; optional introduction with a direct workspace bypass |
| [03 Film Room](directions.html#film) | Expressive type and court colors settle into a dense working interface | Search `Jules`, arrows/Enter to pick, Undo, then See the breakdown | Most useful for daily work; less visual spectacle |

Use **Show design foundation** to remove prominent photography, Fastbreak branding and selected sports details. Then **Add basketball identity** to compare. The toggle exposes the composition; it is not a proposal for a second product brand. Some illustrative court shapes and functional vocabulary remain to make the interaction understandable.

The studies are distinct design directions, not three colors of the same layout. Courtside and Walk the Floor are possible public entrances; Film Room tests their relationship to the practical draft workflow. They can be combined if the transitions and identity remain coherent.

## What changed from the rejected concept

- Replaced the generic dashboard-led public composition with an illustrated scene and an explorable sequence.
- Reused the existing local Anton font for display typography; body and data remain a readable system sans.
- Created original SVG court, hoop, ball and figure artwork. No source-site characters, illustrations, videos, logos or implementation code were copied.
- Added reversible, user-driven interaction with direct controls and visible exits. No autoplay, forced intro, scroll hijacking or timed photo carousel.
- Kept glass on a few navigation controls. Dense tables and evidence remain opaque, with 28-pixel rows.
- Used the supplied Curry and LeBron photos only as local mood references. No photo is attached to a fictional player's identity or moved into the production bundle.

## Browser evidence

[Machine-readable Playwright report](directions-report.json) · [reproducible offline check](check-directions.cjs).

- **18 tested layout states:** three directions × 1440/390/320-pixel widths × foundation/basketball layers.
- **12 full-page captures:** desktop and phone for each direction and layer. Photo-bearing captures stay in the ignored `inspiration-resources/react-review-v2/` folder.
- **14 checks passed:** scene buttons/keyboard/status, drag, identity toggle, navigation/Back/skip focus, journey bounds, dialog/index focus and Escape, reduced motion, hold cancellation, keyboard draft pick/undo, empty search/active option validity, research navigation, layout/labels/images/row height, phone scene overlap/ticket containment, and browser/network errors.
- No horizontal page overflow in the checked states, no missing reference images, no browser runtime errors, no external HTTP requests in the offline run. Hidden journey scenes are inert.
- Visually reviewed representative desktop and phone screenshots and corrected duplicated foundation copy, display-font application, the narrow-screen progress control, and photo/ticket overlap in later journey scenes.

To repeat with an existing Playwright installation and installed Chromium:

```sh
PLAYWRIGHT_MODULE=/absolute/path/to/playwright node docs/plans/visual-review/check-directions.cjs
```

The check installs nothing, opens local files only, and needs no database. The photo files and local project font must be present for image/font review. The browser may require the same OS/sandbox permission used in the previous review session.

## Boundaries and remaining decisions

This is an isolated HTML/CSS/JavaScript design artifact, not a React implementation. Fixture picks are local and do not recompute rankings; evidence remains an explicitly fictional example. Exploration and photo landmarks are public-page concepts, not new engine capabilities.

The directions use deliberately different palettes. Full light/dark variants, authentication and all production route states follow selection; neither the theme requirement nor any existing accessibility or draft gate is relaxed. This pass has not established WCAG conformance, screen-reader behavior, cross-browser support, real mobile-device gesture behavior, or production performance.

The earlier [54-view Blazor audit](round-1-review.md) remains useful baseline evidence: closed-registration CTAs and phone Context Review overflow still need attention. No production defect was fixed in this design-only round.

**Review decisions:**

1. Which should lead the public experience: Courtside's illustrated scene or Walk the Floor's exploration?
2. Is this degree of playfulness and condensed sports typography appropriate, or should the visual tone be more mature?
3. Does Film Room preserve enough of that identity while remaining comfortable to use?

The photo permissions/replacement decision, final public rendering choice, dependency approval and production migration are still pending. The repository gate remains blocked by the previously observed `SSH.NET` 2025.1.0 NU1903 restore errors; no pin or gate was changed. New artifacts remain uncommitted under the repository's green-gate commit rule.

[Archived round-1 gallery — rejected appearance](round-1-gallery.html).
