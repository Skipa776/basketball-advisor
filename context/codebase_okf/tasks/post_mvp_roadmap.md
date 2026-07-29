---
type: task
title: Post-MVP Roadmap
description: What is deliberately out of MVP scope, the constraints each item inherits, and the order to build them in.
tags: [task, roadmap, scope]
source_paths: []
test_paths: []
depends_on: [../safety/scraping_policy.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Owns the boundary of the MVP. Everything here is **out of scope** for the
current build. It is written down so that scope is explicit rather than
remembered, and so the constraints already discovered are inherited rather than
rediscovered.

**Do not start any of this, and do not add abstractions in anticipation of it.**
The design doc has full detail for each; this concept records only what the MVP
work already established.

# Deferred, with inherited constraints

### Rolling windows and the riser/faller engine

Needs per-game stat lines. **Basketball-Reference's `robots.txt` disallows
`*/gamelog/`** — the obvious source. The permitted path is `/boxscores/`, which
is roughly 1230 pages per season and, at the 6 requests/minute ceiling in
[scraping_policy](../safety/scraping_policy.md), means a multi-hour rate-limited
background import, not an interactive one. Design for that from the start or the
feature is unbuildable as specified.

The sustainable-vs-unsustainable distinction (minutes and usage moving vs.
shooting percentage moving) is the actual product value here, not the rolling
averages themselves.

### Category-league analyzer

The MVP *scores* categories; it does not *compare* them. Z-scores, league
percentiles, matchup win probability, punt detection, and category-aware draft
recommendations all live here. `TOV` inverts in every comparison — that rule is
already recorded in
[scoring_rules_catalog](../contracts/scoring_rules_catalog.md).

### Draft intelligence (design-doc phase 5)

Survival-to-next-pick probability, ADP variance, tier detection,
recommended-now vs recommended-later. The MVP approximates this with
`MarketValue` and the raw ADP gap; upgrading means adding terms to
[draft_value_contract](../contracts/draft_value_contract.md) and re-checking the
no-double-counting invariant.

### Streaming advisor

Usable games (not scheduled games), daily lineup capacity, acquisition limits,
multi-transaction sequencing. Start with a greedy heuristic before reaching for
dynamic or integer programming.

### Trade analyzer

Before/after roster value, replacement effects, category redistribution.
Depends on the category analyzer for category leagues.

### Provider integrations

Yahoo OAuth first — it has a documented Fantasy Sports API covering basketball.
Sleeper needs NBA support validated endpoint by endpoint; its docs describe
NFL-only behavior in several places. ESPN stays an **optional adapter**, never a
dependency, and private league pages are never scraped
([scraping_policy](../safety/scraping_policy.md)).

### News ingestion and LLM-proposed context events

The [context_event_catalog](../contracts/context_event_catalog.md) vocabulary
and the human-in-the-loop review workflow already exist so a proposer has a
target. The rule that survives from the MVP: **statistical and optimization code
makes the recommendation; a model may extract context or phrase an explanation,
never decide.** Machine-proposed events land as `Proposed`; only a human writes
`Verified`.

### Production concerns

Authentication, Redis, a job framework, containerized deployment, back-tested
weight calibration.

# Order

Trends → category analyzer → streaming → draft intelligence → trades →
providers → news/LLM → production. Each earns its place by making a real
decision better, not by completing the design document.
