---
type: context_index
title: Concept Index
description: Every concept in the bundle, grouped by concern, with the canonical value each one owns.
tags: [index, navigation]
source_paths: []
test_paths: []
depends_on: []
status: partial
last_updated: 2026-09-22
owners: [engineering]
---

# Responsibility

Navigate-by-concern entry point. Routing by *task* is in
[`AGENT_CONTEXT_INDEX.md`](../AGENT_CONTEXT_INDEX.md); this file groups by *subject*
and names what each concept owns.

Requirements are in [`PROJECT_REQUIREMENTS.md`](../../PROJECT_REQUIREMENTS.md):
**R1–R10 are the MVP**, **R11–R23** are the post-MVP epics. Build order and
paste-ready prompts are in `docs/epics/`.

# Contracts — canonical values live here

## MVP

| Concept | Owns |
|---|---|
| [stat_vocabulary](contracts/stat_vocabulary.md) | The stat key catalog. The keystone — parsers, columns, fixtures, goldens all key off it |
| [scoring_rules_catalog](contracts/scoring_rules_catalog.md) | League types, scoring rule schema, the seed league, the category set |
| [player_identity_contract](contracts/player_identity_contract.md) | Canonical identity, name normalization, the matching ladder, ambiguity |
| [provider_contracts](contracts/provider_contracts.md) | Provider interfaces and the canonical source names |
| [provenance_contract](contracts/provenance_contract.md) | The provenance block required on every imported row |
| [projection_pipeline_contract](contracts/projection_pipeline_contract.md) | The four projection records and the per-minute math |
| [draft_value_contract](contracts/draft_value_contract.md) | The DraftValue formula, replacement level, scarcity, weights |
| [context_event_catalog](contracts/context_event_catalog.md) | Event types, scales, default impacts, verification states |
| [recommendation_evidence_contract](contracts/recommendation_evidence_contract.md) | Evidence shape, confidence, no-score-without-evidence |
| [persistence_contract](contracts/persistence_contract.md) | Entity list, EF conventions, migration and precision rules |
| [api_surface](contracts/api_surface.md) | Response envelope, error codes, endpoint semantics |

## Post-MVP

| Concept | Owns | Epic |
|---|---|---|
| [box_score_storage_contract](contracts/box_score_storage_contract.md) | Atomic completed-game snapshots, phase selection and offline parsing | E06 slice |
| [player_performance_api_contract](contracts/player_performance_api_contract.md) | Owned dated performance queries and React evidence | E06 slice |
| [player_heat_contract](contracts/player_heat_contract.md) | Owner-selected descriptive heat and recent performance | E06 slice |
| [player_season_intelligence_contract](contracts/player_season_intelligence_contract.md) | Planned played-game distribution, availability, and season/draft label vocabulary | owner extension |
| [rolling_window_contract](contracts/rolling_window_contract.md) | Windows, the exact three-way production decomposition, trend score | E06 |
| [category_value_contract](contracts/category_value_contract.md) | Z-scores with volume-weighted ratios, win probability, punt ceiling | E07 |
| [draft_intelligence_contract](contracts/draft_intelligence_contract.md) | Survival probability, tiers, the OpportunityCost replacement | E07 |
| [streaming_contract](contracts/streaming_contract.md) | Usable games, streaming value, planning, drop protection | E08 |
| [trade_contract](contracts/trade_contract.md) | Before/after valuation, win-delta, legality, the fairness boundary | E08 |
| [league_import_contract](contracts/league_import_contract.md) | The snapshot, per-provider mapping, the fail-loud rule | E09 |
| [llm_extraction_contract](contracts/llm_extraction_contract.md) | The Claude call, output schema, the verbatim-quote grounding check | E10 |
| [backtest_contract](contracts/backtest_contract.md) | Holdout protocol, metric definitions, weight fitting, the honesty rule | E11 |
| [model_params_contract](contracts/model_params_contract.md) | Versioned offline-fitted parameters, activation, C#/Python goldens | M1 |
| [auth_tenancy_contract](contracts/auth_tenancy_contract.md) | Identity schema, the owned/shared split, isolation enforcement | E04 |
| [design_system_contract](contracts/design_system_contract.md) | Tokens, inventory, the interaction budget, the WCAG floor | E05 |

# Components — subsystem ownership

| Concept | Subsystem | Epic |
|---|---|---|
| [web_ui_react](components/web_ui_react.md) | Incremental React workspace and API integration | migration |
| [domain_model](components/domain_model.md) | Pure domain types and source independence | — |
| [scoring_engine](components/scoring_engine.md) | Stat line + league → fantasy value | done |
| [projection_engine](components/projection_engine.md) | Observed → baseline projection | E02 |
| [context_engine](components/context_engine.md) | Baseline + events → adjusted projection | E02 |
| [draft_engine](components/draft_engine.md) | Session, board, ranking, recommendations | E02 |
| [application_services](components/application_services.md) | Use cases, abstractions, async rules | — |
| [ingestion_pipeline](components/ingestion_pipeline.md) | HTTP, resilience, rate limiting, cache, import runs | E01 |
| [scrapers](components/scrapers.md) | Per-site parsers, URL builders, fixtures, versioning | E01 |
| [persistence](components/persistence.md) | EF Core, migrations, repositories | — |
| [api_host](components/api_host.md) | ASP.NET host, DI, middleware, options | E03 |
| [web_ui_blazor](components/web_ui_blazor.md) | Blazor Server pages and live draft updates | E03 |
| [background_workers](components/background_workers.md) | Differentiated refresh cadences | E03 |
| [identity_and_authorization](components/identity_and_authorization.md) | Identity, the ownership retrofit, the enumerating sweep | E04 |
| [design_system](components/design_system.md) | The Blazor component library built from tokens | E05 |
| [boxscore_importer](components/boxscore_importer.md) | The rate-limited per-game importer | E06 |
| [trend_engine](components/trend_engine.md) | Risers, fallers, free-agent ranking | E06 |
| [category_analyzer](components/category_analyzer.md) | Z-scores, profiles, matchup outlook, punts | E07 |
| [draft_intelligence](components/draft_intelligence.md) | Survival, tiers, category-aware board | E07 |
| [streaming_advisor](components/streaming_advisor.md) | Usable games and the multi-day plan | E08 |
| [trade_analyzer](components/trade_analyzer.md) | Trade evaluation | E08 |
| [league_import_adapters](components/league_import_adapters.md) | Yahoo, Sleeper, CSV behind one snapshot | E09 |
| [llm_context_proposer](components/llm_context_proposer.md) | The extraction adapter and the review queue | E10 |
| [backtest_harness](components/backtest_harness.md) | As-of clamped runner, metrics, weight search | E11 |
| [distribution_and_operations](components/distribution_and_operations.md) | Image, compose, CI, health, metrics, docs | E12 |

# Safety — non-negotiable, `edit_policy: stable_contract`

- [scraping_policy](safety/scraping_policy.md) — host allowlist, permitted paths, crawl rates
- [secrets_policy](safety/secrets_policy.md) — credentials, tokens, configuration
- [data_integrity_policy](safety/data_integrity_policy.md) — immutable baselines, human-gated verification, provenance
- [llm_trust_boundary](safety/llm_trust_boundary.md) — untrusted article text, and the resulting blast radius
- [tenancy_policy](safety/tenancy_policy.md) — default deny, not-found, worker isolation, deletion and export
- [self_host_hardening](safety/self_host_hardening.md) — safe shipping defaults and what the operator owns

# Tasks — repeatable playbooks

- [one_shot_build_plan](tasks/one_shot_build_plan.md) — budget and wrap-up for an unattended build
- [run_quality_gates](tasks/run_quality_gates.md) — the checks and what each protects
- [add_new_data_source](tasks/add_new_data_source.md) — providers and scrapers, incl. compliance
- [run_design_process](tasks/run_design_process.md) — which design skills, in what order, and which need a human
- [run_backtest](tasks/run_backtest.md) — a back-test run, and reading the results honestly
- [release_checklist](tasks/release_checklist.md) — what must be true before publishing
- [post_mvp_roadmap](tasks/post_mvp_roadmap.md) — what is scoped, deferred, and permanently refused

# Tests — what must exist before a subsystem commits

- [required_gates](tests/required_gates.md) — test policy, the row-ID map, isolation rules
- [test_matrix_scoring](tests/test_matrix_scoring.md) — `S-01`–`S-08`
- [test_matrix_projection_draft](tests/test_matrix_projection_draft.md) — `P-`, `C-`, `E-`, `D-01`–`D-09`
- [test_matrix_ingestion_scrapers](tests/test_matrix_ingestion_scrapers.md) — `I-`, `N-`, `L-`, `W-`, `S-10`–`S-14`, `S-30`–`S-34`
- [test_matrix_api_persistence](tests/test_matrix_api_persistence.md) — `A-`
- [test_matrix_player_performance](tests/test_matrix_player_performance.md) — `HP-01`–`HP-05`
- [test_matrix_box_scores](tests/test_matrix_box_scores.md) — `BS-01`–`BS-10`
- [test_matrix_trends](tests/test_matrix_trends.md) — `T-`
- [test_matrix_advanced_decisions](tests/test_matrix_advanced_decisions.md) — `Y-`, `X-`, `R-`, `S-20`–`S-29`, `S-35`–`S-36`
- [test_matrix_auth_tenancy](tests/test_matrix_auth_tenancy.md) — `U-`
- [test_matrix_llm](tests/test_matrix_llm.md) — `M-`
- [test_matrix_backtest](tests/test_matrix_backtest.md) — `B-`
- [test_matrix_ui_design](tests/test_matrix_ui_design.md) — `D-10`–`D-21`

# Maintenance

- [okf_schema](okf_schema.md) — the metadata and maintenance contract for this bundle
- [assumptions](assumptions.md) — current state, deliberate defaults, what is knowingly unenforced

Player heat verification: [test_matrix_player_heat](tests/test_matrix_player_heat.md).
