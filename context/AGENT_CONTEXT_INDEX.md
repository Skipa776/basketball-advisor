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
| Build or change the React workspace | `codebase_okf/components/web_ui_react.md`, `codebase_okf/contracts/api_surface.md`, `codebase_okf/contracts/auth_tenancy_contract.md` |
| Build or change a page | `codebase_okf/components/web_ui_blazor.md` |
| Add a background refresh job | `codebase_okf/components/background_workers.md` |
| Record where a piece of data came from | `codebase_okf/contracts/provenance_contract.md` |
| Handle an API key, token, or password | `codebase_okf/safety/secrets_policy.md` |
| Run the checks before committing | `codebase_okf/tasks/run_quality_gates.md`, `codebase_okf/tests/required_gates.md` |
| Start an unattended build of this project | `codebase_okf/tasks/one_shot_build_plan.md` |
| Understand a decision I disagree with | `codebase_okf/assumptions.md`, then the design doc |
| Add or edit a concept file | `codebase_okf/okf_schema.md` |
| Know what is in scope at all | `codebase_okf/tasks/post_mvp_roadmap.md` — scoped, deferred, and permanently refused |

## Post-MVP (R11–R23) — build order and prompts in `docs/epics/`

| I am about to… | Read first |
|---|---|
| Connect recorded performance API or React views | `codebase_okf/contracts/player_performance_api_contract.md`, `codebase_okf/tests/test_matrix_player_performance.md`, `codebase_okf/components/web_ui_react.md`, `codebase_okf/safety/tenancy_policy.md` |
| Compute descriptive heat or recent points performance | `codebase_okf/contracts/player_heat_contract.md`, `codebase_okf/tests/test_matrix_player_heat.md`, `codebase_okf/contracts/rolling_window_contract.md`, `codebase_okf/contracts/scoring_rules_catalog.md` |
| Compute a previous-season played-game distribution or seasonal/draft label | `codebase_okf/contracts/player_season_intelligence_contract.md`, `codebase_okf/contracts/player_heat_contract.md`, `codebase_okf/contracts/draft_value_contract.md`, `codebase_okf/safety/data_integrity_policy.md` |
| Compute or change a rolling window or trend | `codebase_okf/contracts/rolling_window_contract.md`, `codebase_okf/components/trend_engine.md` |
| Import per-game box scores | `codebase_okf/components/boxscore_importer.md`, `codebase_okf/safety/scraping_policy.md` |
| Parse or persist completed game observations | `codebase_okf/contracts/box_score_storage_contract.md`, `codebase_okf/tests/test_matrix_box_scores.md`, `codebase_okf/contracts/persistence_contract.md`, `codebase_okf/safety/scraping_policy.md`, `codebase_okf/safety/data_integrity_policy.md`, `codebase_okf/contracts/stat_vocabulary.md`, `codebase_okf/contracts/provenance_contract.md`, `codebase_okf/contracts/auth_tenancy_contract.md` |
| Touch category z-scores, profiles, or punts | `codebase_okf/contracts/category_value_contract.md`, `codebase_okf/components/category_analyzer.md` |
| Compute a normal distribution or rank correlation | `codebase_okf/contracts/category_value_contract.md` — one implementation, shared |
| Change survival probability, tiers, or the market term | `codebase_okf/contracts/draft_intelligence_contract.md` — **and `draft_value_contract.md` in the same commit** |
| Work on usable games or a streaming plan | `codebase_okf/contracts/streaming_contract.md`, `codebase_okf/components/streaming_advisor.md` |
| Evaluate a trade | `codebase_okf/contracts/trade_contract.md`, `codebase_okf/components/trade_analyzer.md` |
| Compute an optimal starting lineup | `codebase_okf/components/streaming_advisor.md` — one implementation, shared with trades |
| Import a league from a provider or CSV | `codebase_okf/contracts/league_import_contract.md`, `codebase_okf/components/league_import_adapters.md` |
| Handle an OAuth token | `codebase_okf/safety/secrets_policy.md`, `codebase_okf/contracts/league_import_contract.md` |
| Write any code that calls a language model | `codebase_okf/safety/llm_trust_boundary.md` **first**, then `codebase_okf/contracts/llm_extraction_contract.md` |
| Change what the model may receive, be granted, or produce | `codebase_okf/safety/llm_trust_boundary.md` — `stable_contract`, needs approval |
| Measure projection accuracy or fit a weight | `codebase_okf/contracts/backtest_contract.md`, `codebase_okf/tasks/run_backtest.md` |
| Add an entity that belongs to a user | `codebase_okf/contracts/auth_tenancy_contract.md`, `codebase_okf/safety/tenancy_policy.md` — **the filter and the sweep row are not optional** |
| Add or change an endpoint after auth lands | `codebase_okf/contracts/auth_tenancy_contract.md` — every endpoint authorizes |
| Style anything, or add a component | `codebase_okf/contracts/design_system_contract.md`, `codebase_okf/components/design_system.md` |
| Add a route, link, CTA, or navigation entry | `codebase_okf/components/web_ui_blazor.md` — "The path between pages"; a route with no inbound link does not ship |
| Work on how the app looks or feels to use | `PROJECT_REQUIREMENTS.md` R23, `docs/epics/E13-interface-and-experience.md` — **the current objective** |
| Reach for a CSS framework or component library | `codebase_okf/assumptions.md` — refused twice, with the arithmetic |
| Run the design process or produce `DESIGN.md` | `codebase_okf/tasks/run_design_process.md` — steps 1–3 need a human |
| Build a chart | Load the `dataviz` skill, then `codebase_okf/tasks/run_design_process.md` step 5 |
| Change a shipping default, compose file, or Dockerfile | `codebase_okf/safety/self_host_hardening.md` |
| Add observability or a health endpoint | `codebase_okf/components/distribution_and_operations.md` |
| Publish a version | `codebase_okf/tasks/release_checklist.md` |

## Reading the bundle cold

Start at `codebase_okf/index.md`. It groups every concept by concern and states
which canonical value each one owns.
