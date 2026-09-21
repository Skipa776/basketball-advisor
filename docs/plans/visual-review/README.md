# Fastbreak — continuous animated landing, round 3

**Start here:** [open the landing page](landing.html) · [visual gallery and motion walkthrough](index.html).

The owner selected one continuous scrolling landing page with more animation inspired by Maxima Therapy. This replaces the earlier choice between Courtside and Walk the Floor as the main public entrance. Sam Walks remains a supporting reference for discovery and wayfinding; the other sites remain backups.

The [migration plan](../react-liquid-glass-migration.md) and [reference dossier](../react-liquid-glass-references.md) now reflect that instruction. This is an isolated HTML/CSS/JavaScript design preview, not the React migration or a deployed frontend.

## The continuous page

1. **See the whole court:** oversized typography and a rotating illustrated perspective wheel.
2. **Your league:** a court drawing and a changeable sticker explain why rules matter.
3. **The context:** an editorial photo spread and scrolling depth carry the research story.
4. **Your move:** a physical hanging sign leads into the practical draft workspace.
5. **Find your next move:** a simple closing invitation and return to the top.

Every section is present in normal vertical document flow. Header links are native page anchors. The wheel changes the illustration without changing the route. There is no pinned scroll sequence, wheel-event interception, forced horizontal journey, loading curtain or mandatory animation before navigation. The workspace remains directly reachable from the sticky header and two later CTAs.

## Motion to try

| Interaction | Implementation in this study | Reduced-motion equivalent |
|---|---|---|
| Rotate the hero wheel | Drag horizontally, arrow keys or previous/next buttons; spring rotation can be reversed before it settles | Buttons/keys switch immediately |
| Bounce the basketball | Click or keyboard activation plays one finite bounce | Artwork stays still |
| Court line drawing | Strokes complete as the court enters the viewport | Complete court immediately |
| Section text reveals | Short, once-only opacity/translation on entry; content is never hidden awaiting JavaScript | Text already in place |
| Photo depth and decoration | Bounded scroll-linked translation and decorative rotation; no idle animation loop | Still composition |
| Court sticker | Tap/click/Enter cycles wording with a short scale response | Wording still changes, without scaling |
| Hanging sign | Horizontal drag tracks the pointer, release springs to rest; click/Enter or arrow keys nudge it | Sign stays still; the workspace link is independent |
| Workspace arrow | Hover/focus rounds its shape and turns the arrow | Same visible link without transition |
| Header | Small scroll-progress line and a restrained translucent navigation surface | Progress remains readable; solid fallback for contrast/transparency preferences |

Use **Reduce motion** in the header to compare. The OS reduced-motion setting takes priority and cannot be overridden by this control. Changed preferences settle active springs and cancel effects. The manual preference lasts for the current page load. Animation work stops when the page is hidden; spring frames stop after settling, and scroll work is scheduled only on scroll/resize. Vertical touch scrolling remains available over the draggable artwork.

The source’s motion inventory is documented in [Maxima’s creator account](https://tympanus.net/codrops/2026/04/06/building-the-maxima-therapy-website-react-gsap-and-dabbling-with-ai/). Our illustrations, layouts and motion code are original. No source-site assets/code or new dependencies were added. The concept uses native scrolling, Pointer Events, requestAnimationFrame, IntersectionObserver, CSS and the Web Animations API. Production library selection remains a separate implementation decision.

## Browser evidence

[Playwright report](landing-report.json) · [reproducible offline checks](check-landing.cjs).

- **15 checks passed** across **8 viewport/motion combinations**: 1440, 768, 390 and 320 CSS pixels, each with normal and reduced motion.
- Five continuous sections, one main landmark and one primary heading; no horizontal document overflow, missing images or unnamed buttons/links in those checked states.
- Verified wheel buttons, keyboard, drag and reversal; finite ball animation; anchor focus and sticky-header clearance; scroll effects; sticker activation; sign dragging and settling; manual/OS motion changes; workspace destination; no-JavaScript reading/navigation; and emulated vertical touch scrolling over the wheel.
- No browser runtime errors or external HTTP requests in the offline run. No account, database or server is involved.
- Four full-page stills, a hero capture, three desktop detail captures, and a recorded motion walkthrough are in the ignored `inspiration-resources/react-review-v3/` folder. Stills show the reduced-motion completed composition; the walkthrough demonstrates normal motion.

To repeat using an existing Playwright installation and installed Chromium:

```sh
PLAYWRIGHT_MODULE=/absolute/path/to/playwright node docs/plans/visual-review/check-landing.cjs
```

The check installs nothing. Reference photos and the existing local Anton font must remain in their current folders. Chromium may require the same OS/sandbox permission used for this review.

## Review and implementation boundaries

The key review is now **motion intensity and composition**, rather than choosing a separate public entrance. Scroll the whole page, play with the wheel and sign, then compare reduced motion. The palette, illustrative style, wording and final photographic assets remain open to your visual judgment.

The photographs remain local mood references, not cleared production assets. The workspace CTA opens the previous functional design preview with explicitly fictional players; it does not authenticate, persist picks or invoke the engine. No production routes, components, packages or canonical design values changed.

This pass does not establish WCAG conformance, real-device gesture behavior, all browser support, production frame-rate/load budgets, or the complete light/dark system. Those remain implementation acceptance work, along with both themes and all route states. The dense draft stays opaque, photo-free and free of animated reranking.

The prior Blazor audit’s closed-registration CTA issues and phone Context Review overflow remain unresolved production work. The previously observed full-gate restore failure (`SSH.NET` 2025.1.0 NU1903) is unchanged; the design artifacts remain uncommitted under the green-gate rule.

Earlier evidence: [round-2 directions and checks](round-2-review.md) · [round-2 gallery](round-2-gallery.html) · [round-1 baseline audit](round-1-review.md). Round 1’s appearance remains rejected. Their results are not evidence for the new landing.
