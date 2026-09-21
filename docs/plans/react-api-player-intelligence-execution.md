# Execution plan: React integration, API verification, and league-aware player rankings

**Date:** 2026-09-19  
**Reviewed code baseline:** `a2830e9`, plus the uncommitted design/planning artifacts.  
**Status:** owner selected draft preparation/live draft, an ESPN-default starter profile, and recent scoring above expected average as “hot.” Implementation is underway; [the progress ledger](implementation-progress.md) records tested delivery and remaining work. The original baseline inventory below is retained for context.

The delivery order is: **build the actual React client → connect a useful set of existing .NET features → prove the API/UI flows → supply exact league settings and per-game data → implement and validate player intelligence → expose it in React.** API tests accompany each integration change; the dedicated verification milestone is the acceptance checkpoint, not the first time APIs get tested.

This supplements the [React migration plan](react-liquid-glass-migration.md), rather than replacing its route inventory, architecture and rollback requirements. The selected public design is the [continuous animated landing](visual-review/landing.html). The signed-in workspace remains a dense decision tool.

## Accepted owner direction — September 19

- **Priority:** draft preparation and live draft decisions. The first integrated release must include a real draft board, picks, recommendations and undo—not only a player browser.
- **Initial scoring:** use the published ESPN default points weights as an explicit, editable starter profile. The real league has not been created. Initial team count is **7**, editable to **11 or more** without a special-case limit. Roster slots, draft position and format remain configurable and are not inferred from those weights.
- **Hot:** a player's recent ESPN fantasy-point production exceeds their expected per-game average. A temporary efficiency/shooting streak can count as hot; sustainability is an explanation alongside it, not a requirement for inclusion.
- **Baseline:** establish the player’s average from their first 10 appearances of the season; expand through 30 appearances; thereafter retain the latest 30. The latest three games define the hot window, compared with the baseline established before those three games.
- **Deferred:** Sleeper-specific modes/bonuses/imports, waiver availability and broader provider automation are not prerequisites for this draft-first release. Revisit them after the ESPN draft workflow is useful.

### Selected starter scoring profile

The executable profile now lives in [LeagueSetupCatalog](../../src/FantasyBasketball.Api/Endpoints/LeagueSetupCatalog.cs). React reads it from `/api/leagues/setup`; the independent HTTP golden verifies its arithmetic.

[Published ESPN weights](https://www.espn.com/fantasy/basketball/story/_/id/30296896/espn-fantasy-default-points-league-scoring-explained), checked 2026-09-19. ESPN's `3PM` maps to the existing canonical `FG3M`. All selected fields already exist in `StatKey`; this starter does not require the additional Sleeper bonus fields.

This is user-selected configuration, not a fallback for a league with missing rules. Save a complete versioned rule set when the preparation league is created; later compare/edit it against the real league. Missing or invalid rules still fail validation. Do not replace the repository's unrelated seed league or its goldens. M0 reconciled the catalog wording with the owner's explicit starter-profile instruction, preserving the no-silent-fallback invariant. No additional permission to use the selected profile is needed.

The profile has moved from this planning definition into its single code owner. Do not add a separate ESPN-specific scoring engine.

## 1. What exists and what is left

| Area | Evidence in the repository | Remaining work |
|---|---|---|
| Visual design | Working local HTML/CSS/JavaScript landing and fixture-based workspace study | Convert to React components; replace fixture interactions with real authenticated state; complete production states and assets |
| Production frontend | Blazor Server in `src/FantasyBasketball.Api/Components`; no `src/FantasyBasketball.Web` React application or project `package.json` | Establish approved React/TypeScript tooling, route ownership and .NET asset serving |
| Basic backend | Account, league, player, projection, draft, context and import endpoints | Fill the specific JSON gaps below; preserve authorization and existing behavior |
| Points scoring | `PointsScoringEngine` sums each configured stat × its league weight using C# decimal | Reuse it; validate exact ESPN/Sleeper settings and unsupported scoring types |
| Projection and draft logic | Existing domain/application services and tests | Reuse and expose them; do not replace their formulas with frontend calculations |
| Per-game box scores | Importer concept is `planned`; no implemented per-game importer found | Build the E06 data foundation and resumable background import |
| Hot/rising players | Rolling-window contract and trend engine are `planned`; a visual `TrendBadge` is not an engine | Implement the specified math, evidence, queries and required tests |
| League imports | E09 defines CSV/manual and conditional Sleeper support; not an implemented integration | Explicit ESPN starter/settings first; CSV and validated Sleeper imports are deferred |
| API verification | Existing integration tests and prior Blazor audit; recent 15-check report covers the static landing | Run fresh integration and browser checks against a real local .NET host and disposable data; current runtime/provider health is unverified |
| Build gate | Previously observed `SSH.NET` 2025.1.0 NU1903 restore failure | Reproduce and fix the dependency issue within allowed pins; never suppress the gate to proceed |

The central distinction: **we have a frontend design to port, not a React application waiting only for an API URL.** There is also useful C# scoring code already in place; player intelligence is an extension of that engine, not a second scoring system.

## 2. Proposed first useful release

Selected draft-first workflow:

1. Open the animated landing, sign in, and restore the session on reload.
2. Create an editable preparation league with the selected ESPN starter scoring; start with seven teams and configure slots/draft details. Allow the preparation setup to grow to eleven or more teams. A real ESPN league connection is not required.
3. Browse/search actual imported players and inspect a league-specific projection with its evidence.
4. Start/reopen a draft, record a pick, undo the latest pick and refresh without losing server state.
5. See real data freshness/import status, empty states and failures.
6. After the integration milestone passes, add **Hot / above expected** and Performance views, plus recent-form evidence on the draft board. Sustainability remains a secondary explanation.

Registration controls must reflect instance configuration. Keep the current context review, data administration and account features available through their existing routes until their React replacements pass parity checks. Do not claim a completed migration while these still depend on Blazor.

Trade analysis, automated transactions, category rankings, league standings, and a full streaming/lineup optimizer are outside this first delivery. A list of high-performing players belongs in Players; it does not turn the unbuilt league-standings page into an implemented capability. For draft preparation, consider the player pool; during the draft, availability comes from recorded draft picks. League free-agent availability is deferred and must not be confused with an undrafted player.

## 3. Milestones and exit criteria

| Milestone | Tasks and concrete output | Completion evidence | Depends on |
|---|---|---|---|
| **M0 — Agree scope and restore a green baseline** | Answer the decision questions; inventory dirty work; reproduce/fix restore failure; approve tooling versions; record the React transition, ESPN starter profile and selected E06 work in the spec stack | Gate passes; versions and selected scope are recorded; old visual work preserved | Owner choices affecting scope |
| **M1 — React foundation** | Create the real web project; port the landing and shell; shared tokens, motion cleanup, routing, typed API boundary, loading/error states; serve built assets from ASP.NET Core | Production build served by .NET; direct URL/reload works; anonymous assets load; normal/reduced motion tested | M0 |
| **M2 — Connect basic features** | Add bootstrap/session, capabilities, league-list and draft-detail contracts; connect account, league, player/projection and draft flows in small end-to-end slices | Real saved league and pick survive reload; undo and errors use server truth; no fixture data in production flows | M1 and corresponding API contracts |
| **M3 — Prove API and UI integration** | Execute the verification matrix below with an isolated PostgreSQL database and real local HTTP host; capture endpoint results and Playwright evidence | Every selected route has success/failure proof; ownership/CSRF pass; no console errors or unexpected failed requests; owner reviews real data UI | M2 |
| **M4 — ESPN rules and game-data foundation** | Validate the selected starter profile and editable setup; build E06 box-score parsing, storage, progress and resumable import; defer external league adapters/CSV snapshots | Published ESPN example calculations match; game rows are trustworthy and sufficient; actual league rules remain editable | M3; data-source preflight |
| **M5 — C# performance and trend logic** | Implement contract-defined windows, performance aggregates and above-expected heat; add trend decomposition/sustainability as explanation, with versioned evidence and league-owned endpoints | Required trend tests pass; independently checked actual scoring; no leakage across periods or leagues | M4; fixture-based engine work can start before a live import completes |
| **M6 — React ranking experience** | Add Hot/Performance views and draft-board recent-form details, window selector, sorting, expected-versus-actual evidence, sample and freshness; retain projected draft value as the main recommendation | A completed-game import changes the displayed rankings; changing rules changes scores; missing data never looks like zero performance | M5 |
| **M7 — Acceptance and controlled migration** | Historical validation, final route parity, production build smoke test, documentation/status updates, rollout and rollback rehearsal | Agreed examples and workflows pass; owner visual review; no unresolved release-blocking gate | M6 and remaining React parity work |

Keep changes reviewable: one meaningful feature slice and its tests/contracts per green commit. If blocked on an external source, continue with recorded fixtures and the selected editable starter settings rather than creating fake live integrations. Completing the selected slices does not complete all of E09 (which also includes Yahoo), E11 calibration, or the entire deployment epic.

## 4. React ↔ .NET integration design

Use React + TypeScript and a Vite build, served by the existing ASP.NET Core host. The production browser uses one origin for assets and `/api`. During development, proxy `/api` to .NET instead of weakening CORS or cookie security. Pin the actual supported toolchain during M0; this plan does not silently add packages.

Keep ASP.NET Identity cookies and the existing antiforgery mechanism. Obtain the antiforgery token before cookie-authenticated mutations and refresh it after authentication changes. React owns interaction state; C# owns scores, projections, picks, authorization and persistence. Generate or centrally define transport types from deliberate C# contracts; validate response/error shapes at the boundary. Do not copy stat catalogs or numeric formulas into JavaScript.

Incremental route ownership must be explicit: retain unmigrated Blazor paths; mount the first React workspace on an isolated review path; transfer final routes one at a time after parity. The SPA fallback must never swallow `/api`, real 404s or remaining Razor routes. Keep a reversible route/asset switch; database migrations stay backward compatible during coexistence. React effects must cancel requests, observers, pointer capture and animation frames when views unmount.

### Endpoint worklist

Paths marked **proposed** are contract-design tasks, not implemented routes.

| Feature | Existing routes | Missing/adaptation work |
|---|---|---|
| Sign-in/out and CSRF | `/api/account/antiforgery`, `/login`, `/logout`, `/register` under `/api/account` | **Proposed** session endpoint and anonymous-safe capability flags; first-owner/registration availability; expired-session recovery |
| League setup and rules | `POST /api/leagues`, `GET /api/leagues/{id}`, `PUT /api/leagues/{id}/scoring` | **Proposed** ownership-filtered `GET /api/leagues`; active-league JSON adapter or compatible current form flow; league-setup update contract for team-count/roster changes; bounded lists and canonical catalogs |
| Player research | `GET /api/players`, `GET /api/players/{id}`, `GET /api/players/{id}/projection?leagueId=…` | Consume actual paging and decomposition; cancel obsolete searches; clear stale results on league change |
| Draft | Create, board, picks, undo and recommendations under `/api/drafts` | **Proposed** `GET /api/drafts/{id}` for complete reloadable session/turn/pick state; reconcile uncertain mutation outcomes |
| Context review parity | List/create/verify/reject under `/api/context-events` | Existing impact-override service needs a JSON mutation contract before its React screen replaces Blazor |
| Import and source status | `/api/imports/*`, `/api/imports/runs`, `/api/health/data-sources` | Preserve owner-only import actions; add box-score progress and league-snapshot preview/apply as scoped contracts |
| Observed performance | None found | **Proposed** `GET /api/leagues/{id}/players/performance` with contract-defined window, through-date, sort, filters and paging |
| Sustainable trends | None found | **Proposed** `GET /api/leagues/{id}/players/trends` with evidence, sample and model/rule provenance; no unproven player in the risers result |

New routes update `ARCHITECTURE.md`, `api_surface.md`, tenancy metadata and tests in the same implementation commit. Reuse existing response/error envelopes. Never authorize access using only the active-league cookie or a React route guard.

## 5. Verify that APIs work, not just that they return 200

Two different checks are needed: **our application's integration** and **the external data source's current availability**. Passing one does not establish the other.

### Automated application checks — entirely offline

Use a disposable test database and a real local .NET HTTP host. External HTTP is blocked or replaced with recorded fixtures. No test touches a developer database or a real fantasy account.

| Scenario | Required proof |
|---|---|
| Bootstrap and account | Anonymous, signed-in, expired session, first owner and registration-closed behavior; correct JSON rather than an HTML login redirect |
| CSRF and ownership | Missing/invalid token rejected; second user cannot read or mutate another user's league/draft/rankings; new owned routes covered by the tenancy sweep |
| League lifecycle | Create → retrieve → change rules → retrieve after process/reload; invalid/duplicate stats rejected with field errors |
| Player query | Filters, pagination beyond page one, empty results, missing IDs and unavailable projections; decimal/date/enum/null handling |
| Draft lifecycle | Create → board → pick → updated recommendations → refresh → latest-pick undo; duplicate retry is idempotent, competing pick conflicts, older-pick undo rejected |
| Failures | Controlled 400/401/403/404/409/429/503 and unexpected failure paths as applicable to each route; no secrets, stack traces or silent stale success |
| Import health | A failed run is visibly failed even when HTTP is 200; a health response distinguishes stale/degraded sources from fresh sources |
| New ranking endpoints | Exact scoring under two different leagues, pagination, window bounds, stable ties, evidence completeness, missing-data behavior and ownership |
| React browser journey | Real login and league/pick persistence; keyboard navigation; account/league-switch cancellation; phone layout; reduced motion; reload/deep links; no mock interception of our own API in the end-to-end acceptance test |

Publish a machine-readable endpoint report with method/path, expected/actual status, envelope/schema check, semantic assertions, duration, timestamp and fixture/database identity. Add Playwright traces/screenshots on failure. Set a latency target after measuring the agreed local dataset; do not invent a green threshold from the static prototype.

### Separate operator smoke checks for live providers

Only after adapter and source-policy preflight: make a small, read-only request against an allowed source; verify schema, NBA identity, relevant season/date and freshness; record redacted evidence and supported capabilities. No automated test suite makes these calls. Respect current limits and backoff. A skipped live check is reported as **not verified**, never green. Do not submit transactions or scrape a private ESPN league.

## 6. ESPN first; future provider compatibility

The owner has explicitly selected the ESPN starter profile above. When the real league exists, reconcile its actual settings against that saved profile. The provider name alone must never override the saved rules. The existing [scoring catalog](../../context/codebase_okf/contracts/scoring_rules_catalog.md) forbids provider defaults as fallbacks. Empty rules are invalid. Domain math receives canonical rules and does not branch on platform names.

| Platform | First integration | Important difference |
|---|---|---|
| ESPN | Manual configuration plus the application's documented CSV template | No authenticated ESPN scrape or new private API integration under the current contract; verify all configured weights and roster settings |
| Sleeper | Manual/CSV first; read-only NBA import only for endpoints validated with sanitized, committed NBA fixtures | Public documentation still describes NFL-only support for some resources; do not assume changing a URL to `nba` establishes support |

Sleeper currently describes **Lock-In**: a completed player performance can be selected for that week before the player's next game. This is different from summing every game in the week. Its scoring options also include double-double/triple-double and technical/flagrant-foul rules. [Format](https://support.sleeper.com/en/articles/4701979-intro-to-sleeper-fantasy-basketball), [scoring](https://support.sleeper.com/en/articles/4645009-what-scoring-settings-are-available), [API documentation](https://docs.sleeper.com/).

ESPN describes configurable points-based scoring. Confirm the actual league configuration rather than inferring it from a platform label. [ESPN scoring format](https://support.espn.com/hc/en-us/articles/4669578914324-Scoring-Format).

### Future compatibility gaps — do not block the selected ESPN starter

- `StatKey` currently has standard counting/ratio fields, but no double-double, triple-double, technical-foul or flagrant-foul fields. Do not silently discard those bonuses/penalties. Extend the canonical vocabulary, source coverage, rules and tests if the user's league needs them; otherwise report exact support only for the represented settings.
- Missed-shot rules require an explicit, tested mapping from attempts/makes. Preserve stacking with other rules. Reject settings that cannot be represented exactly.
- Game-based bonuses must be evaluated on actual game rows before aggregating a window. A bonus computed from averaged season stats is wrong. Future bonus projections require an explicit probability/model approach; observed bonus support alone does not establish correct projected bonus value.
- `LineupCadence` is currently only Daily/Weekly. It cannot silently stand in for Lock-In or Game Pick. Per-game points rankings can be useful first, but full lock-in/weekly decision support needs an explicitly scoped contract and engine extension. Do not call ordinary weekly total rankings Sleeper-compatible decision advice.
- Provider registration/provenance must extend the canonical provider catalog rather than scatter new strings through the domain.
- CSV import uses preview → diff → confirmation → revision, with manual overrides highlighted. Unknown player identities remain pending matches; fuzzy guesses must not contaminate rankings.

## 7. Define “hot” and “best performing” separately

Owner-aligned product vocabulary:

| View | Question answered | Proposed calculation/behavior |
|---|---|---|
| **Performance** | Who has actually scored the most under my rules? | Score each completed game in C#, then expose total and per-game average for the selected window; show games/minutes played and data completeness |
| **Hot / above expected** | Who is producing more fantasy points than their usual expectation? | Recent average minus an explicitly identified expected per-game average; keep efficiency-driven streaks eligible, display sample and uncertainty |
| **Trends / sustainable risers** | Who is improving for reasons more likely to persist? | Implement the existing rolling-window decomposition and `TrendScore`; distinguish changes in minutes, usage and efficiency |
| **Projected value** | Who is expected to help next? | Use existing projection/draft services with baseline, adjustment, final value, uncertainty and freshness; forecast horizon is a separate decision |

Hot is not the existing `TrendScore`. The owner explicitly means recent scoring uplift, including streaks that may regress. The current stable trend contract continues to govern **sustainable risers**; its exclusion of `Unproven` players must not be weakened to populate a Hot list. Hot needs a separately specified, descriptive performance comparison in the API/evidence contract.

**Proposed initial heat definition:**

- `RecentAverage`: arithmetic mean of completed-game fantasy scores in the selected window, under the saved league rule revision.
- `ExpectedAverage`: an explicitly named benchmark fixed before that window; its source and cutoff appear beside the value.
- `AboveExpected`: `RecentAverage - ExpectedAverage`, in fantasy points per game. Sort the Hot view by this absolute uplift. Show recent scoring beside it so a low-output player's improvement is not mistaken for elite production.
- Show games above expectation, games played, game dates and the individual-game scores; this distinguishes a repeated streak from one outlier. Binary badge thresholds/minimum samples remain to be specified before implementation, not guessed in React.
- Percentage uplift is optional and only defined for a positive expected denominator. Missing expectation gives “insufficient baseline,” not an assumed zero. No positive evidence gives an honest empty hot list.

### Owner-selected expanding baseline, then rolling 30 games

The benchmark is an observed player average, not a model forecast. Let `n` be the number of completed, valid regular-season appearances available for that player at the baseline cutoff:

| Available appearances at cutoff | Baseline |
|---|---|
| Fewer than 10 | Insufficient baseline; show sample progress, not a hot score |
| 10 through 30 | Average fantasy points from appearances 1 through `n` |
| More than 30 | Average fantasy points from appearances `n − 29` through `n` |

Examples: after appearance 10, use games 1–10; after 20, use 1–20; after 30, use 1–30; after 31, use 2–31; after 40, use 11–40. Every new eligible game expands or slides the baseline accordingly. Score every included game under the same saved league rule revision before averaging.

“Games” means the player's actual recorded appearances, not the team's scheduled games, calendar days or games missed through injury/DNP. Ignore incomplete games. A trade does not restart the player's same-season history; a new season does. Missing source rows must be identified as a coverage problem rather than treated as missed appearances or zero scores. Preseason/playoff results are not silently mixed into this baseline.

**Owner-selected short hot-streak window: the latest three appearances.** Calculate the reference baseline at the instant just before that window starts, applying the owner's expanding/rolling rule to the appearances available then. The owner selected three games in response to the question presenting this non-overlapping comparison. The three recent games cannot inflate their own expectation.

For example, after game 13, compare games 11–13 against games 1–10. After game 33, compare games 31–33 against games 1–30. After game 34, compare games 32–34 against games 2–31. After game 40, compare games 38–40 against games 8–37. The independently displayed **current rolling average** would use 11–40; label those two averages with their actual date ranges so they cannot be confused. An individual new game can first be compared after ten prior games; the complete three-game hot comparison first exists after appearance 13. Until then, expose progress/actual performance without inventing a full-window heat score.

This owner instruction supersedes the plan's earlier uncapped season-average proposal for **Hot / above expected**. It does not authorize changing baseline projections or silently replacing the separate stable `TrendScore` contract. During M0/M5, record the descriptive heat baseline and its versioned parameters in their single owning contract/options file; retain the existing sustainable-trend semantics unless explicitly revised. The existing `WindowSpan` does not include three games: add the required short-window representation to its canonical contract and tests during implementation rather than pretending `Last5Games` means three. A 30-game baseline is not `Last30Days`. Neither the short-window nor baseline constants may be repeated in React.

**Illustrative UI, fictional numbers:** recent 45 points/game versus prior average 35 → **+10 above average**, with dates, sample size and a separate “efficiency-driven” explanation if applicable. That is hot under the owner's definition even if the model doubts it will last.

**Team-count behavior:** seven is an initial setting, not a constant in draft mathematics or UI layout. Test seven, eleven and a larger supported count. League size affects draft order, user-slot validation, board capacity and replacement-level calculations, so re-read/recompute the applicable server state after setup changes. The current session snapshots team count and user slot: preserve an in-progress draft’s recorded configuration and picks. A changed preparation league must not silently rewrite an existing draft; starting a new draft from the revised setup is the proposed safe path. No salary-cap implementation is implied by configurable team count.

**Draft behavior:** the main board continues to rank using projected/draft value and its existing evidence. Heat is an additional column/filter/research cue, not an automatic boost in the draft formula. Prevent double-counting recent performance already reflected in projections. Any later heat-based recommendation adjustment requires its own contract change and historical validation.

**Preseason/offseason behavior:** do not market the last games of a past season as a current streak. Show a dated historical-form view, or no current hot badge when the chosen recency/freshness policy is not met. Do not mix preseason, playoffs and regular-season samples silently. Rookies without an adequate baseline can still appear in draft projections, but do not receive invented heat scores.

### C# implementation sequence

1. Import and resolve completed per-game rows, with game/player identity, dates, minutes, canonical stats and provenance. Store idempotently and expose progress/failures. Season averages alone cannot produce genuine recent-form rankings.
2. Build the required short windows from [rolling_window_contract.md](../../context/codebase_okf/contracts/rolling_window_contract.md) and the separately owned expanding/rolling heat baseline specified above. Existing sustainable-trend formulas and thresholds remain authoritative; do not treat 30 games as 30 days.
3. Calculate observed fantasy scores with the configured league rules. Raw-stat windows may be shared, but league-specific fantasy values must be evaluated with the correct rule revision. Never reuse one league's computed score for another league.
4. Compute the separately specified above-expected performance comparison. Then implement the existing minutes/usage/efficiency decomposition, sample weighting, role-risk term and evidence. Exclude the recent window from its baseline. Treat zero minutes/usage, inadequate baseline and missing inputs according to the contract; never substitute an invented zero-risk score for unavailable evidence.
5. Query performance/riser results on demand from the windows. Rebuild derived windows after an import; do not persist an authoritative ranked leaderboard. Rule changes must affect the next query without stale cached values.
6. Return period bounds, through-date, sample size, total/average, expected-benchmark value/source/cutoff, above-expected difference, relevant trend terms, sustainability, provenance/freshness and model/rule revision. Observed aggregates and future projections use distinct fields and labels.
7. Test and compare against independently calculated target-league examples before connecting React.

Do not multiply per-game projections by scheduled games and call that “best” for every platform. Forecast horizons, availability, playable roster slots and Sleeper modes require their own semantics. The initial preparation universe is the player pool; the live draft view filters recorded drafted players. The hot subset requires sufficient comparison data. “Available to add” requires current league rosters; without them, do not label a player a free agent.

### Correctness and usefulness gates

- Heat-specific tests: repeated above-average games, an efficiency-only streak, a one-game outlier, a weak player with large percentage but small absolute gain, missing/zero/negative expectation, stale prior-season results, rule changes applied to both periods, and cutoff leakage. Add boundary cases at 9/10/11 and 29/30/31 baseline appearances, exact dropped/added games, long injury gaps, team trades, season reset, and the first eligible full three-game window at appearance 13. Explicit fixtures must prove the game-33/game-34 baseline transition and the absence of overlap. A heat score must not silently alter draft recommendations.
- Required E06 rows: T-01…T-06, T-10…T-14 and box-score S-30…S-34, with shared source-policy tests. If free-agent buckets are deferred, report the E06 slice as partial rather than claiming the entire epic passes.
- Add direct disjoint-date assertions to the baseline test; a constant-production example alone cannot prove the baseline excludes the window.
- Draft setup tests cover seven teams, eleven teams and a larger valid count; snake turn reversal, valid user slots, board/pick bounds, team-count-sensitive replacement values, and preservation of existing draft snapshots after league setup changes.
- Cover no games, one-game spikes, zero usage/minutes, negative fantasy values, tied ranks, duplicate imports, postponed/incomplete games, early season, offseason, stale seasons, missing bonus inputs, changed league rules and separate users/leagues.
- Never include future games or corrections unavailable at the chosen evaluation cutoff in a backtest. Record the supported correction/re-import policy explicitly; the present importer contract forbids silently re-fetching completed games.
- Reproduce actual fantasy totals on owner-confirmed examples from the target league. That validates scoring fidelity, not prediction accuracy.
- For claims of sustainable future value, evaluate chronologically held-out periods against simple recent/season-average baselines through E11. Keep uncalibrated weights labelled as such; establish a success metric and sample before tuning. No guaranteed accuracy claim.

## 8. Known dependencies and scope decisions

1. **Frontend scope:** draft preparation/live draft is selected; finalize the permanent public rendering choice. Recommended deployment is the existing single .NET origin; preserve public/auth HTML during transition. SPA-only versus prerendered public React remains an owner decision from the migration plan.
2. **Source-policy discrepancy:** the box-score concept/epic permits `/boxscores/`, but the stable safety allowlist table and current URL-builder implementation list only season/player paths. Resolve this recorded discrepancy under the safety change procedure and reverify current source directives before any live box-score fetch. Do not bypass the URL builder or use `/gamelog/`. Offline parser/engine work can continue meanwhile.
3. **Provider feasibility:** an editable local ESPN preparation profile is the first version; no external league is required. CSV/provider adapters follow later. Sleeper NBA compatibility needs real response evidence. A public Sleeper API description is not a promise of supported NBA league access or unrestricted commercial use.
4. **Data latency:** the current data architecture is batch-oriented. Recommend completed-game refresh and visible freshness first. Live in-game rankings require a separately verified source, budget and refresh design.
5. **Images:** the current local athlete photos remain reference-only. Choose cleared assets or original illustration before publishing; visual approval does not establish photo permission.
6. **Repository scope:** E13 is currently the interface-only objective. The selected next stages bring R11/E06 and the explicit ESPN starter profile into the sequence; broader R16/E09 integration is deferred. Reconcile the recorded objective and contracts during M0; do not weaken stable safety or scoring rules.

## 9. Decisions made and remaining

**Resolved:** draft preparation/live draft first; ESPN-default points starter; no existing external league; seven teams initially with growth to eleven or more; an average established at ten games, expanding through thirty, then rolling over thirty; the latest three games compared with the baseline before them; hot means production above that historical expectation, not necessarily sustainable improvement. Do not ask the owner to choose these again.

**Remaining details:**

**Draft setup:** roster slots, user draft position and snake versus salary-cap format remain unspecified. Team count is seven, configurable upward. The existing engine uses snake ordering; retain that as the proposed initial supported format and keep setup fields editable. Do not imply ESPN scoring weights determine the draft format. Check the existing draft engine's supported format before promising salary-cap support.

**Proposed delivery assumptions, changeable without delaying the initial API work:** single-origin self-hosted deployment; completed-game rather than live-stat refresh; preparation across the player pool and live availability from manually recorded picks. Live draft assistance does not imply automated ESPN draft synchronization. Paid data, deployment audience/SEO, final photo rights and a calendar deadline remain open if they matter to launch. Budget/source-policy decisions must precede live data acquisition.

**Success demonstrations:** prepare a ranked ESPN-scored pool; start a draft, enter picks and see the available recommendations change; undo a pick and recover after reload; identify a player above their prior/expected average and inspect whether the increase is minutes, usage or efficiency driven. No new league/provider account is needed for these demonstrations.

## 10. Next action after the decisions

Execute M0, then build the smallest real vertical slice: **React sign-in → session restore → league selection/setup → league-specific player detail**, verified through real local .NET HTTP calls and persistence. Then complete **start/reopen draft → enter pick → recommendations update → undo → reload recovery** before the first integrated owner handoff. Broader provider integrations remain deferred. No ranking should be presented as live until its underlying game data and exact scoring have passed their own gates.

This planning pass inspected code/contracts and official platform documentation. It did not implement React, exercise the running application APIs, import live data, or certify provider availability. Current evidence is a plan grounded in the repository, not a connectivity test result.


### 2026-09-20 projection/draft implementation checkpoint

The React workspace now publishes projections from explicitly chosen imported
season data, shows server-ranked shortlist/evidence/confidence and full player
projection detail, and refreshes after pick/undo. Saved values are isolated by
league, calculation run and scoring profile; changed rules require explicit
recalculation. This advances M2/M3 and draft-value presentation in M6; it does not
complete per-game importing, heat API/UI, landing parity or release. The current
verification and exact next action are in [implementation progress](implementation-progress.md).
