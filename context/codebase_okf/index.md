---
type: context_index
title: Concept Index
description: Every concept in the bundle, grouped by concern, with the canonical value each one owns.
tags: [index, navigation]
source_paths: []
test_paths: []
depends_on: []
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Navigate-by-concern entry point for this bundle. Routing by *task* is in
[`AGENT_CONTEXT_INDEX.md`](../AGENT_CONTEXT_INDEX.md); this file groups by
*subject* and names what each concept owns.

The whole bundle describes the MVP in
[`PROJECT_REQUIREMENTS.md`](../../PROJECT_REQUIREMENTS.md). Every concept starts
`status: planned` because no code exists yet.

# Contracts — canonical values live here

| Concept | Owns |
|---|---|
| [stat_vocabulary](contracts/stat_vocabulary.md) | The stat key catalog. The keystone — parsers, columns, fixtures, goldens all key off it |
| [scoring_rules_catalog](contracts/scoring_rules_catalog.md) | League types, scoring rule schema, the seed league values, the category set |
| [player_identity_contract](contracts/player_identity_contract.md) | Canonical player identity, name normalization, match and ambiguity rules |
| [provider_contracts](contracts/provider_contracts.md) | Provider interface signatures and the canonical source names |
| [provenance_contract](contracts/provenance_contract.md) | The provenance block required on every imported row |
| [projection_pipeline_contract](contracts/projection_pipeline_contract.md) | The four projection records and the per-minute math |
| [draft_value_contract](contracts/draft_value_contract.md) | The DraftValue formula, replacement level, scarcity, and the weight constants |
| [context_event_catalog](contracts/context_event_catalog.md) | Event types, direction, magnitude and confidence scales, impact clamps |
| [recommendation_evidence_contract](contracts/recommendation_evidence_contract.md) | Evidence shape, confidence levels, and the no-score-without-evidence rule |
| [persistence_contract](contracts/persistence_contract.md) | Entity list, EF conventions, migration and precision rules |
| [api_surface](contracts/api_surface.md) | Response envelope, error codes, endpoint contracts |

# Components — subsystem ownership

| Concept | Subsystem |
|---|---|
| [domain_model](components/domain_model.md) | Pure domain types and the source-independence rule |
| [scoring_engine](components/scoring_engine.md) | Stat line + league → fantasy value |
| [projection_engine](components/projection_engine.md) | Observed → baseline projection |
| [context_engine](components/context_engine.md) | Baseline + events → adjusted projection |
| [draft_engine](components/draft_engine.md) | Draft session, board, ranking, recommendations |
| [application_services](components/application_services.md) | Use cases, repository abstractions, async rules |
| [ingestion_pipeline](components/ingestion_pipeline.md) | HTTP, resilience, rate limiting, cache, import runs |
| [scrapers](components/scrapers.md) | Per-site parsers, URL builders, fixtures, parser versioning |
| [persistence](components/persistence.md) | EF Core, migrations, repositories |
| [api_host](components/api_host.md) | ASP.NET host, DI, middleware, options |
| [web_ui_blazor](components/web_ui_blazor.md) | Blazor Server pages and live draft updates |
| [background_workers](components/background_workers.md) | Differentiated refresh cadences |

# Safety — non-negotiable, `edit_policy: stable_contract`

- [scraping_policy](safety/scraping_policy.md) — host allowlist, permitted paths, crawl rates
- [secrets_policy](safety/secrets_policy.md) — credentials, tokens, configuration
- [data_integrity_policy](safety/data_integrity_policy.md) — immutable baselines, no auto-verified context, required provenance

# Tasks — repeatable playbooks

- [one_shot_build_plan](tasks/one_shot_build_plan.md)
- [run_quality_gates](tasks/run_quality_gates.md)
- [add_new_data_source](tasks/add_new_data_source.md)
- [post_mvp_roadmap](tasks/post_mvp_roadmap.md)

# Tests — what must exist before a subsystem commits

- [required_gates](tests/required_gates.md)
- [test_matrix_scoring](tests/test_matrix_scoring.md)
- [test_matrix_projection_draft](tests/test_matrix_projection_draft.md)
- [test_matrix_ingestion_scrapers](tests/test_matrix_ingestion_scrapers.md)
- [test_matrix_api_persistence](tests/test_matrix_api_persistence.md)

# Maintenance

- [okf_schema](okf_schema.md) — the metadata and maintenance contract for this bundle
- [assumptions](assumptions.md) — current state, deliberate defaults, revisit triggers
