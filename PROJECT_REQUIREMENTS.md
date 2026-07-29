# PROJECT_REQUIREMENTS.md

The MVP is done when all ten stories below pass their acceptance criteria.
Rationale for each lives in the design doc; this file is canonical for *what
must be true*.

Scope note: this is design-doc phases 0–4. Streaming, trades, category analysis,
riser/faller trends, and provider OAuth are **out of scope** — see
`context/codebase_okf/tasks/post_mvp_roadmap.md`.

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
