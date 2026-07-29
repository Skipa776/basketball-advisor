# AGENT_INSTRUCTIONS.md — build sequence

Contracts outward. One commit per step. Before starting a step, read the
concepts it names. Before committing a step, its **required** test-matrix rows
must have real tests and the gate must be green.

`scripts/gate.sh` runs the `[commands]` from `stack_config.toml` in order. From
step 1 onward, it is the definition of "green".

---

### 1 — Skeleton and gates
Solution, 4 src projects + 3 test projects per `ARCHITECTURE.md`, `global.json`,
`Directory.Build.props`, `Directory.Packages.props` (every version from
`stack_config.toml`, nowhere else), `compose.yaml`, `.editorconfig`,
`.gitignore`, `scripts/gate.sh`, `packages.lock.json` committed for each project.
**Done when:** `dotnet restore --locked-mode` succeeds, `scripts/gate.sh` is
green on an empty solution, `docker compose up -d --wait` brings up Postgres 17.
Read: `tasks/run_quality_gates.md`.

### 2 — Stat vocabulary and league configuration
`StatKey`, `StatLine`, `ScoringRule`, `FantasyLeague`, `LeagueType`,
`RosterSlot`, plus league validation.
**Done when:** every `StatKey` member matches `contracts/stat_vocabulary.md`
exactly, and an invalid league is rejected with a field-naming error.
Read: `contracts/stat_vocabulary.md`, `contracts/scoring_rules_catalog.md`,
`components/domain_model.md`.

### 3 — Scoring engine  *(the first real milestone)*
Points and category scoring. Given a stat line and a league, return fantasy value.
**Done when:** the seed-league golden cases in `tests/test_matrix_scoring.md`
pass, including the negative-turnover and missing-stat cases. **R5 is satisfied
here** — do not proceed until it is.
Read: `components/scoring_engine.md`, `tests/test_matrix_scoring.md`.

### 4 — Persistence
`FantasyDbContext`, entity configurations, initial migration, repositories
behind the `Application/Abstractions` interfaces.
**Done when:** migrations apply to a Testcontainers Postgres and round-trip a
league, a player, and a season stat line.
Read: `contracts/persistence_contract.md`, `components/persistence.md`.

### 5 — Identity and provenance
`PlayerIdentityResolver` (normalization, matching, ambiguity → human
confirmation), `DataProvenance` stamped on every imported row.
**Done when:** the ambiguity cases in the identity contract resolve to a pending
match rather than a wrong link.
Read: `contracts/player_identity_contract.md`, `contracts/provenance_contract.md`.

### 6 — Ingestion pipeline
`IHttpClientFactory` wiring, resilience handler, per-host rate limiter, response
cache, `DataImportRun` recording, degraded-mode behavior.
**Done when:** the rate limiter provably holds at or below the configured
ceiling, and a provider failure produces a failed `DataImportRun` with prior
data intact.
Read: `components/ingestion_pipeline.md`, `safety/scraping_policy.md`.

### 7 — balldontlie provider  *(R3)*
Teams, players, games. API key from configuration.
**Done when:** contract tests map recorded JSON to canonical models, and the
free-tier rate ceiling is respected.
Read: `contracts/provider_contracts.md`, `components/ingestion_pipeline.md`.

### 8 — Basketball-Reference scraper  *(R4)*
Season per-game, totals, and advanced tables. URL builder restricted to
allowlisted paths.
**Done when:** parser tests pass from saved fixtures with zero network calls,
a malformed table fails loudly, and the URL builder rejects a disallowed path.
Read: `components/scrapers.md`, `safety/scraping_policy.md`,
`tests/test_matrix_ingestion_scrapers.md`.

### 9 — ADP
FantasyPros `/nba/adp/` scraper, plus CSV import and manual entry as fallback
rungs. All three write the same canonical ADP record.
**Done when:** an ADP value is available with the scraper disabled.
Read: `components/scrapers.md`, `contracts/provider_contracts.md`.

### 10 — Projection engine
Per-minute rates from observed stats, minutes projection, multiply out,
persist `ObservedStats` and `BaselineProjection` as separate records.
**Done when:** the worked example in the projection contract reproduces exactly,
and no code path writes to an existing `BaselineProjection` row.
Read: `contracts/projection_pipeline_contract.md`, `components/projection_engine.md`.

### 11 — Draft engine  *(R6, R7, R10)*
Session, board, manual picks with undo, replacement level, positional scarcity,
roster fit, ADP market value, ranked recommendations with evidence.
**Done when:** a full mock draft runs end to end from manual entry only, the
board re-ranks after every pick within the R7 latency target, and every
recommendation carries at least one evidence item.
Read: `components/draft_engine.md`, `contracts/draft_value_contract.md`,
`contracts/recommendation_evidence_contract.md`, `tests/test_matrix_projection_draft.md`.

### 12 — Context engine  *(R8, R9)*
`ContextEvent` + `PlayerContextImpact` CRUD, human verification workflow,
`ContextApplier` producing `AdjustedProjection` from a baseline.
**Done when:** applying and then removing a context event leaves the baseline
byte-identical, expired events stop applying, and nothing reaches `Verified`
without an explicit human action.
Read: `components/context_engine.md`, `contracts/context_event_catalog.md`,
`safety/data_integrity_policy.md`.

### 13 — API and Blazor UI  *(R1, R8 display)*
Endpoints per `ARCHITECTURE.md`, exception middleware, options binding, then the
pages: Dashboard, My League, Players, Draft Assistant, Context Review, Data
Sources.
**Done when:** a league can be created, a draft run, and a projection
decomposition viewed, entirely through the UI.
Read: `contracts/api_surface.md`, `components/api_host.md`, `components/web_ui_blazor.md`.

### 14 — Background workers
Schedule, stats, and ADP refresh at their differentiated cadences. Not one timer
for everything.
Read: `components/background_workers.md`.

### 15 — Close out
Integration tests green, coverage minimums met, forbidden-pattern scan clean,
every concept `status` synced to reality, `python3 context/validate_okf.py`
clean, `assumptions.md` updated.

---

## Budget ladder

If you cannot finish, protect this order and never reorder it:

1. Steps 1–3 — skeleton, vocabulary, scoring engine with tests
2. Steps 4–9 — persistence, identity, ingestion, both scrapers
3. Steps 10–11 — projections and a working draft board
4. Step 12 — context engine
5. Steps 13–15 — UI breadth, workers, polish

A working, tested draft board beats a broad, broken surface. UI breadth is the
first thing to sacrifice; tests for what exists are the last.

## Completion criteria

The build is complete when all ten stories in `PROJECT_REQUIREMENTS.md` pass
their acceptance criteria, `scripts/gate.sh` is green, and every concept status
reflects what is actually in the tree.
