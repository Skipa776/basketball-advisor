# Fastbreak: UI/UX references and photo dossier

**Research dates:** 2026-09-18–19. Companion to the [migration plan](react-liquid-glass-migration.md).

These references explain specific design choices. They are not templates to copy or proof that a proposed Fastbreak feature already exists. Public vendor pages/documentation were researched; the local images listed below were visually inspected. No authenticated third-party product was tested, and no reference-site performance or accessibility certification is claimed.

## Owner-selected primary references — round 2

The owner rejected the Arena Glass study as too recognizably AI-generated and directed the next pass toward these two sites first, with basketball identity added after the visual/interaction foundation. Public pages were inspected in a browser, including Maxima's settled illustrated hero and Sam Walks' entry into the walking scene. No source code or source-site artwork is incorporated.

### Maxima Therapy — expressive scene and physical interaction

- [Live site](https://maximatherapy.com/)
- [Creator's implementation account](https://tympanus.net/codrops/2026/04/06/building-the-maxima-therapy-website-react-gsap-and-dabbling-with-ai/)

The creator documents a rotating illustrated hero, interactive SVGs, water ripples and physics effects. Claude helped with bounded implementation tasks; the project credits human art direction, branding and illustration. The round-2 **Courtside** study translates the large illustrated scene and tactile navigation into original court geometry and perspective changes. It uses CSS/SVG and the existing local font without adding the source site's libraries.

### September 19 update — Maxima motion becomes the lead

The owner selected a continuous scrolling landing page with more animation. The creator’s account above was revisited for its documented wheel, scroll interactions, hanging physics block, morphing CTA and clickable stickers. The [new landing](visual-review/landing.html) adapts those ideas with original basketball artwork, a spring-driven sign and wheel, a finite ball bounce, court strokes, photo parallax, text reveals and a rounded CTA transition. It does not reproduce the source’s characters, physics implementation, water simulation or Lottie assets.

These are analogous behaviors, not a claim of matching Maxima’s production polish. Native scrolling replaces the source’s smoothing stack in this dependency-free concept. No GSAP, Lenis, Matter.js, Lottie, framework or component library was added. Sam’s optional horizontal journey remains a historical alternative; its wayfinding lessons still inform the continuous page. See the [motion inventory and limitations](visual-review/README.md).

### Sam Walks — exploration and visible progress

- [Live experience](https://sam-walks-site.vercel.app/)
- [Creator's workflow and community feedback](https://www.reddit.com/r/ClaudeDesign/comments/1u2xts6/how_to_turn_a_static_website_into_an_immersive/)

The creator describes developing a design system before handing the project to AI coding tools and making later mobile fixes. The live experience uses a walking character, scene landmarks, progress and an index. The **Walk the Floor** study translates that relationship into a court-side journey, with an original figure, photo landmarks and a direct bypass to the workspace. It copies neither the source's videos nor the illustrated character.

### Backups, in the order of the problem they solve

- [SimplyRaffle](https://simplyraffle.com/): coherent, committed visual identity. [Mixed discussion with several positive reactions](https://www.reddit.com/r/ClaudeAI/comments/1u2mcwl/can_i_see_your_claude_built_websites/).
- [Webharu's AI build account](https://www.webharu.com/en/ai-making): comparing design options and human review before implementation; creator-reported outcomes.
- [Konny Amaya's portfolio process](https://konnyamaya.com/en/portfolio/mi-portfolio): explicit typography, layout and interaction handoff, followed by small reviewed phases.

These supersede the original shortlist as the primary aesthetic references. Apple remains the material/accessibility reference; the sources below remain supporting research, not equal competing art directions. See [the round-2 study](visual-review/directions.html).

## Reference shortlist

### 1. Apple: material hierarchy and controls

- [Human Interface Guidelines — Materials](https://developer.apple.com/design/human-interface-guidelines/materials)
- [Meet Liquid Glass — WWDC25](https://developer.apple.com/videos/play/wwdc2025/219/)
- [Apple's design introduction](https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/)

**Documented pattern:** a distinct translucent layer for navigation and controls, with appearance adapting to context. The material guidance distinguishes this from content surfaces and advises selective use.

**Proposed Fastbreak adoption:** one coherent glass utility/navigation layer, restrained highlight edges, anchored menus, and readable underlying content. The league selector should feel attached to the workspace it changes. Reference the WWDC chapter on principles when reviewing whether an element should be glass at all.

**Boundary:** native Apple rendering is not a CSS feature. Fastbreak implements a web interpretation with progressive fallback. A glass stat table, full-screen liquid distortion, and illegible ghost buttons would contradict the intended hierarchy.

### 2. Linear: dense information with quiet navigation

- [A calmer interface for a product in motion](https://linear.app/now/behind-the-latest-design-refresh) — March 12, 2026
- [How we redesigned the Linear UI](https://linear.app/now/how-we-redesigned-the-linear-ui)

**Documented pattern:** consistent headers and action locations, quieter sidebar styling, compact navigation, and reduced competing decoration. The articles include before/after illustrations and describe their reasoning.

**Proposed Fastbreak adoption:** separate global league/account context from page-specific filters; keep page actions in predictable positions; let the player table carry more weight than navigation. Use a compact evidence detail area rather than a dashboard of equally loud cards.

**Boundary:** borrow hierarchy and consistency, not Linear's exact colors, icons, copy, or project-management concepts. Fastbreak's primary action is a basketball decision, not issue creation.

### 3. Nike Basketball: sports editorial rhythm

- [Nike Basketball](https://www.nike.com/basketball)

**Observed on the public page:** distinct basketball campaign sections, short display headlines, and focused actions grouped around individual stories/products.

**Proposed Fastbreak adoption:** one basketball image and one message per public-page section; vary a large image-led entrance with quieter explanatory content. Give the photography enough space to establish the sport without adding fictitious statistics.

**Boundary:** no storefront, product promotions, copied athlete imagery, Nike branding, or unearned performance claims. Campaign content is dynamic; use the structure as a reference, not its current claims as product facts.

### 4. NBA digital experience: relevant entry points

- [NBA's reimagined app announcement](https://www.nba.com/news/nba-launches-reimagined-app-a-destination-for-nba-fans-of-every-team) — historical reference, September 27, 2022

**Documented pattern:** personalized entry points and basketball media organized around fan interests.

**Proposed Fastbreak adoption (our design inference):** foreground the selected league and the actions this user can actually take. A league change must update the visible context consistently across pages.

**Boundary:** this is a historical product-direction reference, not a claim about today's authenticated interface. No news feed, highlights, live scores, personalization engine, NBA ID integration, or league branding is added by borrowing that organizational principle.

### 5. Raycast: discoverable keyboard actions

- [Action Panel](https://manual.raycast.com/action-panel)
- [Keyboard shortcuts](https://manual.raycast.com/keyboard-shortcuts)

**Documented pattern:** contextual actions are searchable, labelled with shortcuts, and operable with Enter and Escape.

**Proposed Fastbreak adoption:** visible keyboard hints beside pick search and undo, an accessible list with a clear active option, and predictable Escape/focus behavior. An optional navigation palette could expose existing destinations without adding data capabilities.

**Boundary:** do not intercept browser/system shortcuts indiscriminately. A command palette is optional; the draft must be fast without opening one. No AI chat or extension platform is proposed.

### 6. Marvel Rivals: an unmistakable first viewport

- [Official site](https://www.marvelrivals.com/)
- Local screenshot: `inspiration-resources/homepage.png`

**Visually inspected local pattern:** dominant artwork, large title hierarchy, a prominent primary action, and a strong navigation band. The local screenshot is a historical reference, not a capture of the current website.

**Proposed Fastbreak adoption:** use one athlete/action composition to establish basketball immediately, then move quickly into what the tool actually does. Keep the title and CTA dominant even when the photo has strong contrast.

**Boundary:** replace the game's angular/yellow visual language with the selected Fastbreak material system. Do not copy logos, characters, artwork, partner rows, autoplay behavior, or download-oriented calls to action.

### 7. Riot sign-in: separate immersion from form clarity

- [Riot account entry](https://account.riotgames.com/) — destination reference only; no sign-in was performed
- Visually inspected source: `inspiration-resources/Login.png`
- Saved reference: `inspiration-resources/loginInspo.html`

**Local screenshot pattern:** an isolated readable sign-in form surrounded by immersive artwork. The form is visually calmer than the background.

**Proposed Fastbreak adoption:** an athlete composition beside a near-opaque auth form, with glass limited to surrounding navigation or small controls. On mobile, the form takes priority over the artwork.

**Boundary:** the saved HTML contains third-party scripts and tracking/configuration. It is reference material, not a component source, and was not executed. Social-login buttons, cookies banners, and recovery links must not be copied unless Fastbreak actually supports the underlying behavior.

## Feature selection summary

| Proposed feature | Source of inspiration | Fastbreak destination | Scope |
|---|---|---|---|
| Layered navigation and league switcher | Apple | Public header / workspace utility region | Recommended |
| Stable page headers and quieter side navigation | Linear | All authenticated routes | Recommended |
| Basketball editorial hero and varied section rhythm | Nike + local Marvel reference | Landing | Recommended, asset-dependent |
| Immersive image with a calm form | Local Riot reference | Login/register | Recommended, asset-dependent |
| Contextual keyboard hints | Raycast | Draft and review actions | Recommended |
| Navigation command palette | Raycast | Global shell | Optional decision |
| Selected-league relevance | NBA organizational direction | Home and league context | Existing capability, improved presentation |
| Player detail split view/sheet | Fastbreak's decomposition task | Players | Proposed layout decision |
| Mobile glass dock | Apple's control hierarchy | Phone navigation | Optional decision |

## Local asset inventory

Dimensions were read from the actual files, not inferred from filenames. No licence was inferred from the appearance of an image.

| File | Dimensions | Visually observed content | Proposed design use | Shipping state |
|---|---|---|---|---|
| `inspiration-resources/Curry.jpeg` | 399 × 501 | Monochrome portrait/action moment of Stephen Curry, upper-body emphasis, dark crowd | Compact auth-side reference; tight editorial portrait, preserve face and arms | Reference-only under current docs; source/licence unresolved; too small for a large crisp hero |
| `inspiration-resources/Lebron.jpg` | 682 × 1024 | Monochrome airborne LeBron James with ball and basket | Vertical action reference for a landing-side composition; preserve ball/face/hoop relationship | Reference-only under current docs; source/licence unresolved; seek larger cleared original for desktop |
| `inspiration-resources/homepage.png` | 5120 × 2880 | Marvel Rivals landing screenshot | Hierarchy, image scale and CTA placement reference | Do not ship screenshot or extract its artwork |
| `inspiration-resources/Login.png` | 1415 × 1384 | Riot sign-in screenshot with art surrounding a white form | Form/art separation and mobile prioritization reference | Do not ship screenshot or copy unsupported sign-in options |
| `inspiration-resources/hovereffect.png` | 144 × 55 | Angular black/yellow navigation item treatment | Understand the requested sense of a responsive nav item; reinterpret as a restrained material/underline state | Tiny UI screenshot, not a reusable graphic; keyboard focus must be equally clear |
| `inspiration-resources/loginInspo.html` | HTML | Saved Riot sign-in document; inspected as text only | Reference provenance/context | Third-party runtime content; never import or execute as app code |
| `inspiration-resources/marvel_rivals.html` | HTML | Saved Marvel Rivals document; inspected as text only | Reference provenance/context | Third-party scripts/content; never import as app code |
| `resources/nophotoforplayer.jpg` | 612 × 612 | Existing placeholder file; dimensions inspected, visual use not required for the proposal | None; existing initials fallback is preferable | `ASSETS.md` records the earlier shipped placeholder removal for missing provenance; do not revive without evidence |

The folder's presence does not clear redistribution rights. `PRODUCT.md` and `ASSETS.md` explicitly identify the two athlete photos as mood references that cannot currently ship. `inspiration-resources/` is also intentionally Git-ignored. Record cleared replacements or new permission before moving any derivative into frontend public assets.

### Crop review checklist

- Desktop and phone crop both preserve the relevant action, with no stretched aspect ratio.
- Text and focus rings remain readable at the lightest and darkest image areas.
- A face/ball/hand never sits behind a form label or primary action.
- An intentional fallback is visible while loading and on failure.
- An identifiable portrait never mislabels a fictional or different player.
- Available source pixels support the chosen display size and density; a soft reference is not accepted as a sharp final hero.
- Attribution, modification terms, and derivative records are complete before publication.

## Technical references for implementation

These support architecture choices, not aesthetic claims:

- [React — build an app from scratch](https://react.dev/learn/build-a-react-app-from-scratch): a bundler alone does not supply routing and data-fetching architecture.
- [React Router — picking a mode](https://reactrouter.com/start/modes): Data Mode supplies loaders/actions/pending state while retaining control of bundling and server integration.
- [Vite — backend integration](https://vite.dev/guide/backend-integration): manifest-driven assets and development integration with a separate backend.
- [MDN — backdrop-filter](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/Properties/backdrop-filter): web backdrop behavior and backdrop-root considerations.
- [MDN — reduced transparency](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/@media/prefers-reduced-transparency): OS preference signal; explicit in-app fallback is still proposed.
- [ASP.NET Core — antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0): retain the cookie/token boundary when JavaScript submits mutations.

No external code, images, or styles were downloaded into the application as part of this research.
