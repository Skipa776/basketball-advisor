---
type: task
title: Post-MVP Roadmap and Permanent Non-Goals
description: What is now scoped as an epic, what remains deferred, and what is deliberately never being built.
tags: [task, roadmap, scope]
source_paths: [docs/epics]
status: planned
last_updated: 2026-07-29
owners: [engineering]
depends_on: [../safety/scraping_policy.md]
test_paths: []
---

# Responsibility

Owns the boundary of the project. Everything that was deferred at MVP has now
either been **scoped as an epic** with full concepts and a build prompt, or is
listed below as still-deferred or permanently out of scope.

# Now scoped — R11–R23

Requirements: [`PROJECT_REQUIREMENTS.md`](../../../PROJECT_REQUIREMENTS.md).
Build prompts and the dependency graph: `docs/epics/`.

| Was deferred | Now |
|---|---|
| Rolling windows / risers-fallers | R11 — [rolling_window_contract](../contracts/rolling_window_contract.md) |
| Category analyzer | R12 — [category_value_contract](../contracts/category_value_contract.md) |
| Draft intelligence (survival, tiers) | R13 — [draft_intelligence_contract](../contracts/draft_intelligence_contract.md) |
| Streaming advisor | R14 — [streaming_contract](../contracts/streaming_contract.md) |
| Trade analyzer | R15 — [trade_contract](../contracts/trade_contract.md) |
| Provider integrations | R16 — [league_import_contract](../contracts/league_import_contract.md) |
| News → context events via LLM | R17 — [llm_extraction_contract](../contracts/llm_extraction_contract.md) |
| Weight calibration / accuracy | R18 — [backtest_contract](../contracts/backtest_contract.md) |
| Auth | R20 — [auth_tenancy_contract](../contracts/auth_tenancy_contract.md) |
| Deployment | R21 — [distribution_and_operations](../components/distribution_and_operations.md) |
| Interface quality and the path between pages | R23 — [web_ui_blazor](../components/web_ui_blazor.md) — **current objective** |

The `/boxscores/` constraint that blocked rolling windows is resolved and owned by
[boxscore_importer](../components/boxscore_importer.md): `*/gamelog/` is
robots-disallowed, `/boxscores/` is permitted at roughly 3.5 hours per season, and
that latency is designed for rather than worked around.

# Still deferred — not scoped, not refused

Worth building eventually; no concepts written, because writing them now would be
specifying code nobody is about to write.

- **Redis and horizontal scale.** A single-instance self-hosted app with an
  in-process cache is the shipping target. Revisit if a hosted multi-tenant
  deployment ever happens.
- **Live draft co-op.** Multiple people watching one draft board. Blazor Server's
  circuit makes it plausible; nobody has asked.
- **Mobile-native app.** The responsive web UI covers the draft-night phone case.
- **Injury-return and minutes ML models.** The per-minute heuristic plus context
  events is the current answer; a real model needs the back-test harness first to
  prove it beats the heuristic.
- **Natural-language roster Q&A.** Considered and declined at scoping: the answer
  path would have to be grounded strictly in engine output, and the explainability
  guarantee is easier to keep with structured evidence than with generated prose.
- **League standings and opponent rosters.** The domain models *your* league's
  rules and *your* roster. There are no opponent teams, no head-to-head matchups,
  no weekly results, and therefore no standings to rank. Every requirement in
  R1–R23 is about deciding, not about tracking a season. Named here because the
  Leaderboard surface exists as a designed shell (row `D-33`) and has to point at
  something real when it says what is missing.

  **This is not the cross-tenant aggregate refused below.** The other managers in
  a fantasy league are not users of this instance; their teams would be imported
  data hanging off the owner's own league record, isolated by the same filter as
  everything else. The two look similar in a sentence and are different in the
  schema.

# Permanently out of scope

Not "later" — decided against.

- **Automated transactions.** No submitting waiver claims, adds, drops, or trades to
  any provider. This is why Yahoo integration requests **read-only** scope
  ([league_import_contract](../contracts/league_import_contract.md)). A tool that
  recommends and a tool that acts are different products with different failure
  modes, and the failure mode of the second is losing someone's season to a bug.
- **Authenticated or private-page scraping.** Any provider, any page, any
  justification ([scraping_policy](../safety/scraping_policy.md)).
- **An LLM in the recommendation path.** A model may extract context or phrase an
  explanation. Statistical and optimization code makes every recommendation
  ([llm_trust_boundary](../safety/llm_trust_boundary.md)).
- **Cross-tenant aggregates.** No "other managers in your instance" anything
  ([tenancy_policy](../safety/tenancy_policy.md)).
- **Microservices and Kubernetes.** A modular monolith is the right shape for this
  problem and this deployment target.
- **Judging trade fairness for the other side.** No model of their needs exists, and
  a verdict computed without one would be authoritative-looking noise
  ([trade_contract](../contracts/trade_contract.md)).

# Verification

Nothing to verify — this concept exists so scope is written down rather than
remembered. A feature request that is not in R1–R23 and not on the deferred list
above is a scope change, and it gets discussed before it gets built.
