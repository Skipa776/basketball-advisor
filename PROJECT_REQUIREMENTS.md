# PROJECT_REQUIREMENTS.md

The MVP is done when all ten stories below pass their acceptance criteria.
Rationale for each lives in the design doc; this file is canonical for *what
must be true*.

**R1–R10 are the MVP** (design-doc phases 0–4). **R11–R22, below, are the
post-MVP epics** that take the project to a self-hostable, publishable product.
Each is scoped as one epic with its own prompt in `docs/epics/`. Nothing outside
R1–R22 is in scope — see `context/codebase_okf/tasks/post_mvp_roadmap.md`.

---

### R1 — Create a custom league manually

*As a fantasy manager, I configure my league's exact rules so that every number
the app shows is computed under my rules, not a provider's defaults.*

- Points and category league types are both configurable via API and UI.
- Scoring values, roster slots, position eligibility, bench/IR slots, team
  count, and lineup cadence (daily/weekly) are all user-set.
- No ESPN/Yahoo default is ever assumed as a fallback value.
- A league with zero scoring rules is rejected with a validation error naming
  the missing field.
- Owning concepts: `contracts/scoring_rules_catalog.md`, `components/domain_model.md`

### R2 — Store NBA players and recent statistics

- Players, NBA teams, season stat lines, and the game schedule persist in
  PostgreSQL through EF Core migrations.
- Every player has an internal canonical id; provider ids are stored as
  secondary identities, never as the primary key.
- Owning concepts: `contracts/player_identity_contract.md`, `contracts/persistence_contract.md`

### R3 — Ingest at least one data source over HTTP

- The balldontlie provider imports teams, players, and games.
- Requests go through `IHttpClientFactory` with resilience and rate limiting;
  the API key is read from configuration, never from source.
- A failed import is recorded as a failed `DataImportRun` and does not corrupt
  previously imported data.
- Owning concepts: `components/ingestion_pipeline.md`, `contracts/provider_contracts.md`

### R4 — Ingest at least one permitted public source by HTML scraping

- The Basketball-Reference scraper imports season per-game, totals, and advanced
  tables for a given season.
- Only allowlisted paths are requested, at or below the configured crawl rate.
- Parser tests run entirely from saved HTML fixtures — no network.
- A changed page structure fails a parser test rather than writing wrong data.
- Owning concepts: `components/scrapers.md`, `safety/scraping_policy.md`

### R5 — Calculate league-specific player value

- Given a stat line and a league's scoring rules, the engine returns that
  player's fantasy value under those rules.
- Points and category leagues are both supported.
- This is the first milestone: it must work, with tests, before anything
  downstream is built.
- Owning concepts: `components/scoring_engine.md`, `contracts/scoring_rules_catalog.md`

### R6 — Run a draft with manually entered picks

- A draft session is created from a league, draft position, and round count.
- Picks are entered manually via a fast search/select UI and are never dependent
  on any integration being available.
- A pick can be undone; the board returns to its exact prior state.
- Owning concepts: `components/draft_engine.md`

### R7 — Dynamically rerank the remaining pool

- After every pick, the board recomputes available players, positional scarcity,
  value above replacement, and roster fit.
- Rankings reflect the *user's* roster construction, not just absolute value.
- Ranking recomputation for a full player pool completes fast enough to be usable
  mid-draft (target: under 500 ms).
- Owning concepts: `components/draft_engine.md`, `contracts/draft_value_contract.md`

### R8 — Show baseline, context adjustment, and final projection separately

- Observed stats, baseline projection, adjusted projection, and fantasy value are
  four distinct persisted records.
- Applying context never mutates or overwrites the baseline.
- The UI displays the decomposition, not just the final number.
- Owning concepts: `contracts/projection_pipeline_contract.md`, `safety/data_integrity_policy.md`

### R9 — Record role and context events

- A user can create, accept, reject, or adjust the confidence of a context event
  and its per-player impacts.
- No inferred or scraped event is ever `Verified` without a human action.
- An event has an effective window; expired events stop affecting projections.
- Owning concepts: `components/context_engine.md`, `contracts/context_event_catalog.md`

### R10 — Generate explainable draft recommendations

- Every recommendation carries structured evidence and a confidence level.
- A recommendation with zero evidence items is a bug, not a display choice.
- The recommendation distinguishes best-available from best-value-at-this-pick
  using ADP.
- Owning concepts: `contracts/recommendation_evidence_contract.md`, `components/draft_engine.md`

---

## Cross-cutting requirements

- **Source independence.** The domain never learns whether data came from an
  API, a scraper, a CSV, or a human. Enforced by an architecture test.
- **Degraded operation.** Any provider being unavailable reduces confidence and
  surfaces staleness in the data-source health view; it never breaks the app.
- **Manual override.** Rosters, scoring, picks, player status, context events,
  and projection adjustments are all user-overridable.
- **Provenance.** Every imported row records where it came from and when.

---

# Beyond the MVP — the publishable product

Ordered by dependency, not priority. Each maps to one epic prompt in
`docs/epics/`.

### R11 — In-season trends (risers and fallers)

*As a manager mid-season, I want to know who is genuinely breaking out versus who
is just shooting hot.*

- Rolling 7/14/30-day and last-5/last-10-game windows per player.
- Each trend decomposes production change into **opportunity** (minutes, usage)
  versus **efficiency** (shooting percentages), and labels sustainability from
  that split — not from the size of the production change.
- Small samples are shrunk, not flagged after the fact.
- Requires per-game stat lines, which requires the `/boxscores/` importer —
  `*/gamelog/` is robots-disallowed. See `safety/scraping_policy.md`.
- Owning concepts: `contracts/rolling_window_contract.md`, `components/trend_engine.md`, `components/boxscore_importer.md`

### R12 — Category analyzer

- Per-category z-scores across the player pool, with **ratio categories weighted
  by volume** (a 90% free-throw shooter on two attempts is not an asset).
- Team category profile as percentiles, expected weekly totals, and per-category
  matchup win probability.
- Punt detection: identify categories that are unreachable and recompute player
  value with them excluded.
- `TOV` inverts everywhere.
- Owning concepts: `contracts/category_value_contract.md`, `components/category_analyzer.md`

### R13 — Draft intelligence

- Probability a player survives to the user's next pick, from ADP and its
  variance — replacing the MVP's raw ADP gap.
- Tier detection: value cliffs in the remaining pool.
- Category-aware draft value for category leagues.
- Owning concepts: `contracts/draft_intelligence_contract.md`, `components/draft_intelligence.md`

### R14 — Streaming advisor

- **Usable** games, not scheduled games: a game counts only if an eligible
  lineup slot is actually open that day after better players are placed.
- Multi-day add/drop sequences under a weekly acquisition limit.
- Owning concepts: `contracts/streaming_contract.md`, `components/streaming_advisor.md`

### R15 — Trade analyzer

- Points leagues: before/after roster value including replacement backfill and
  slot displacement.
- Category leagues: before/after category profile and change in expected
  matchup wins.
- Multi-player and multi-team trades.
- Owning concepts: `contracts/trade_contract.md`, `components/trade_analyzer.md`

### R16 — Provider integrations

- Yahoo OAuth import of league, roster, and scoring settings.
- Sleeper after per-endpoint NBA validation; ESPN via CSV and manual only.
- Every provider stays an **optional adapter**: with all of them disabled the app
  is fully usable through manual entry.
- Owning concepts: `contracts/league_import_contract.md`, `components/league_import_adapters.md`

### R17 — LLM-proposed context events

- An article becomes one or more **`Proposed`** context events typed against the
  existing catalog, for a human to accept, edit, or reject.
- Article text is untrusted input: the model gets no tools, a schema-constrained
  output, and no path to `Verified`.
- Disabled by default; the app is fully functional without an API key.
- Owning concepts: `contracts/llm_extraction_contract.md`, `safety/llm_trust_boundary.md`, `components/llm_context_proposer.md`

### R18 — Back-testing and calibration

*The requirement that converts two documented guesses into measured numbers.*

- Import multiple historical seasons; hold one out.
- Report projection accuracy: MAE, RMSE, Spearman rank correlation, top-K hit
  rate, and calibration by decile.
- Fit the draft weights against realized outcomes and write the fitted values
  into the shipped defaults, with the report committed alongside.
- Owning concepts: `contracts/backtest_contract.md`, `components/backtest_harness.md`

### R19 — Design system and accessibility

- A documented design system — tokens, component inventory, interaction
  budget — that the Blazor components are built from rather than styled ad hoc.
- WCAG 2.2 AA as a gate, not an aspiration. Risk and confidence are never
  conveyed by colour alone.
- The draft board is keyboard-operable end to end.
- Owning concepts: `contracts/design_system_contract.md`, `components/design_system.md`, `tasks/run_design_process.md`

### R20 — Accounts and data ownership

- ASP.NET Identity accounts. Leagues, drafts, and context events belong to a
  user; players, stats, and schedules are shared reference data.
- Every endpoint authorizes; cross-user access is impossible, proven by test.
- First registration claims the instance as owner; open registration is a config
  choice, closed by default.
- Owning concepts: `contracts/auth_tenancy_contract.md`, `safety/tenancy_policy.md`, `components/identity_and_authorization.md`

### R21 — Self-hostable distribution

- Multi-arch container image plus a `docker compose up` quickstart that works
  from a clean clone with no API keys.
- README with screenshots, LICENSE, CI running the full gate, seeded demo data.
- Documented upgrade path: migrations apply forward on an existing volume.
- Owning concepts: `components/distribution_and_operations.md`, `tasks/release_checklist.md`, `safety/self_host_hardening.md`

### R22 — Observability and operations

- Structured logs, health endpoints, and metrics for import success, scrape rate
  consumption, and recommendation latency.
- A data-source health view an operator can act on.
- Owning concepts: `components/distribution_and_operations.md`
