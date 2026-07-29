---
type: component
title: Web UI (Blazor Server)
description: The Blazor Server pages, how live draft updates work, and the rule that the UI formats explanations rather than computing them.
tags: [component, ui, blazor]
source_paths: [src/FantasyBasketball.Api/Components]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [../contracts/recommendation_evidence_contract.md, ../contracts/api_surface.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: low
done_criteria:
  - A league can be created, a draft run, and a projection decomposition viewed through the UI alone.
  - No page displays a score without its evidence.
---

# Responsibility

Owns the user surface. Blazor Server was chosen so there is one language and one
build, and because its SignalR circuit already pushes state to the browser — the
live draft board needs no separate hub, no polling, and no client bundle.

# Pages (MVP)

| Page | Purpose |
|---|---|
| Dashboard | Data freshness, recent imports, quick entry points |
| My League | League setup and scoring configuration |
| Players | Search, filter, and the projection decomposition |
| Draft Assistant | Live board, fast pick entry, ranked recommendations with evidence |
| Context Review | Create, verify, reject, and override context events |
| Data Sources | Per-source health, last success, staleness |

Free Agents, Streamers, Risers/Fallers, and Trades are post-MVP — see
[post_mvp_roadmap](../tasks/post_mvp_roadmap.md). Do not add empty pages for
them.

# Design

- Components call Application services directly through DI. Blazor Server is
  in-process; routing UI calls through HTTP to its own API would add a hop and
  a serialization step for nothing.
- **Fast pick entry is the priority interaction.** Keyboard-first: type, arrow,
  enter. A live draft gives you ninety seconds, and a mouse-driven picker will
  lose the pick.
- The draft board re-renders from recomputed state after each pick; the circuit
  pushes it.

# Invariants

- **The UI formats explanations; it never computes them.** Evidence arrives
  structured from the engine and is grouped and worded here. A percentage
  calculated in a `.razor` file is a bug. *Check: review, plus the engine-side
  evidence tests.*
- **No score is displayed without its evidence.** *Check:
  [recommendation_evidence_contract](../contracts/recommendation_evidence_contract.md)
  row E-01 makes an evidence-free recommendation unconstructible, so the UI
  cannot receive one.*
- **Projections are always shown decomposed** — baseline, adjustment, final —
  never the final number alone. That is requirement R8. *Check: row A-20.*
- **Unverified context is visibly marked as unverified** wherever it affects a
  displayed number. *Check: row A-21.*
- **Degraded sources are visible**, not hidden behind a stale number. *Check:
  the health page test.*

# Change procedure

Adding a page: the component, its route, its service dependency, and an
integration test that renders it — one commit.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows
A-20 and A-21.
