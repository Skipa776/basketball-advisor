# AGENT_CONTEXT_INDEX

Task → concept routing. Find your task, read those concepts **before** editing.
Index rows route; concepts own. Nothing here restates a concept's content.

| I am about to… | Read first |
|---|---|
| Add or rename a statistic | `codebase_okf/contracts/stat_vocabulary.md` — then every fixture, parser map, and golden that keys off it |
| Change scoring values or add a league type | `codebase_okf/contracts/scoring_rules_catalog.md`, `codebase_okf/components/scoring_engine.md` |
| Compute fantasy value from a stat line | `codebase_okf/components/scoring_engine.md`, `codebase_okf/tests/test_matrix_scoring.md` |
| Match a provider's player to ours | `codebase_okf/contracts/player_identity_contract.md` |
| Add a new API data provider | `codebase_okf/tasks/add_new_data_source.md`, `codebase_okf/contracts/provider_contracts.md` |
| Add or fix a scraper | `codebase_okf/tasks/add_new_data_source.md`, `codebase_okf/safety/scraping_policy.md`, `codebase_okf/components/scrapers.md` |
| Touch anything that makes an HTTP request | `codebase_okf/components/ingestion_pipeline.md`, `codebase_okf/safety/scraping_policy.md` |
| Change how projections are computed | `codebase_okf/contracts/projection_pipeline_contract.md`, `codebase_okf/components/projection_engine.md` |
| Apply news or role changes to a projection | `codebase_okf/contracts/context_event_catalog.md`, `codebase_okf/components/context_engine.md`, `codebase_okf/safety/data_integrity_policy.md` |
| Change draft rankings or the pick recommendation | `codebase_okf/contracts/draft_value_contract.md`, `codebase_okf/components/draft_engine.md` |
| Change what a recommendation says or how sure it is | `codebase_okf/contracts/recommendation_evidence_contract.md` |
| Add an entity, column, or migration | `codebase_okf/contracts/persistence_contract.md`, `codebase_okf/components/persistence.md` |
| Add or change an HTTP endpoint | `codebase_okf/contracts/api_surface.md`, `codebase_okf/components/api_host.md` |
| Build or change a page | `codebase_okf/components/web_ui_blazor.md` |
| Add a background refresh job | `codebase_okf/components/background_workers.md` |
| Record where a piece of data came from | `codebase_okf/contracts/provenance_contract.md` |
| Handle an API key, token, or password | `codebase_okf/safety/secrets_policy.md` |
| Run the checks before committing | `codebase_okf/tasks/run_quality_gates.md`, `codebase_okf/tests/required_gates.md` |
| Start an unattended build of this project | `codebase_okf/tasks/one_shot_build_plan.md` |
| Work on streaming, trades, categories, or trends | `codebase_okf/tasks/post_mvp_roadmap.md` — these are **out of MVP scope** |
| Understand a decision I disagree with | `codebase_okf/assumptions.md`, then the design doc |
| Add or edit a concept file | `codebase_okf/okf_schema.md` |

## Reading the bundle cold

Start at `codebase_okf/index.md`. It groups every concept by concern and states
which canonical value each one owns.
