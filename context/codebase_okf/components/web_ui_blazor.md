---
type: component
title: Web UI (Blazor Server)
description: The Blazor Server pages, how live draft updates work, and the rule that the UI formats explanations rather than computing them.
tags: [component, ui, blazor]
source_paths: [src/FantasyBasketball.Api/Components]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [../contracts/recommendation_evidence_contract.md, ../contracts/api_surface.md]
status: partial
last_updated: 2026-07-31
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

# Pages (R23)

The instrument grew a grouped shell and four more routes:

| Page | Route | Backing |
|---|---|---|
| My Leagues | `/leagues` | Real — `LeagueService.ListAsync`, ownership-filtered |
| Trade Analyzer | `/trade` | **None.** R15 / epic E08 — designed shell |
| Free Agents | `/free-agents` | **None.** R14 — designed shell |
| Leaderboard | `/leaderboard` | **None.** Standings are deferred, not scoped — designed shell |

# Designed shells for capabilities that do not exist

This **amends** the former rule "do not add empty pages for them" (2026-07-31,
epic E13). The rule was written to stop half-built routes from reading as done,
and that concern is still right. What changed is that the interface could not be
evaluated with a third of its navigation missing, and a designed surface that
says plainly what is not built is more honest than a nav item that is absent for
reasons a tester cannot see.

The permission is narrow and gated:

- A shell renders `NotBuiltState`, which **names the requirement or roadmap entry**
  it is waiting on. "Coming soon" is not a permitted string; the row is what is
  missing and where it is written down.
- A shell renders **no numeric player or team data**, real or invented. The
  layout may exist; the numbers may not. *Check:
  [test_matrix_ui_design](../tests/test_matrix_ui_design.md) row D-33.*
- A shell is a finished surface. An empty `.razor` file with a TODO is still
  forbidden by `AGENTS.md` and always will be.

**The Trade Analyzer shell does not arbitrate fairness.** Judging a trade for the
other side is permanently out of scope — no model of their needs exists, and a
verdict computed without one is authoritative-looking noise. The surface shows
both rosters and reports what the trade does to *your* team, which is what R15
specifies and what the product is comfortable claiming.

Streamers and Risers/Fallers get no route at all — they are post-MVP with no
surface anyone has asked to test. See
[post_mvp_roadmap](../tasks/post_mvp_roadmap.md).

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

# The path between pages (R23)

This component owns the routes, so it owns the graph they form. The
[design_system_contract](../contracts/design_system_contract.md) owns what a
component looks like and DESIGN.md owns the aesthetic; neither owns whether a
person can get from one screen to the next, which is where the shipped defects
were.

- **Every route is reachable from in-app navigation.** A rendered route is not a
  reached route — `/welcome` proved that with a green suite. *Check:
  [test_matrix_ui_design](../tests/test_matrix_ui_design.md) row D-28.*
- **No call to action dead-ends** under this instance's configuration. A CTA whose
  destination is closed is not rendered as a CTA. *Check: row D-29.*
- **No input demands a value the UI never displays.** Either the identifier appears
  on some screen, or the input is a picker. *Check: row D-32.*
- **Every reachable state is designed** — signed out, demo off, first run, and
  failure — not only the populated path. *Check: rows D-17 and D-25.*

# Change procedure

Adding a page: the component, its route, its service dependency, **the navigation
entry or link that reaches it**, and an integration test that renders it — one
commit. A route with no inbound link does not ship.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows
A-20 and A-21.

# Implementation evidence

The shared Blazor Server host renders the six MVP routes with semantic HTML and
calls scoped Application services directly. My League creates a points league
with its scoring rule; Players searches and renders observed, baseline,
context-adjusted, and final records; Draft Assistant starts a session, records
keyboard-selected picks, reranks, and displays evidence with every score;
Context Review creates proposals and performs verify, reject, and audited
impact-override actions; Dashboard and Data Sources expose persisted import
history and degradation, and Data Sources can queue every MVP import. League
setup accepts points or categories, exact stat rules, roster slots, team count,
and daily/weekly cadence without a provider default. The pick combobox supports
type, up/down, Enter, and
returns focus after a committed pick. A loopback Kestrel render test over real
PostgreSQL proves A-20 and A-21, and a route sweep renders all six pages.
