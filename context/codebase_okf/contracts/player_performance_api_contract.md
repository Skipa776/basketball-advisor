---
type: contract
title: Recorded Player Performance API
description: Owned points-league queries over explicit dated game observations, with paged heat evidence and honest missing-data states.
tags: [contract, api, heat, react]
source_paths: [src/FantasyBasketball.Application/Trends/PlayerPerformanceService.cs, src/FantasyBasketball.Api/Endpoints/PerformanceEndpoints.cs, src/FantasyBasketball.Web/src/performance.tsx]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api/PlayerPerformanceHttpTests.cs, src/FantasyBasketball.Web/tests/workspace.mjs]
depends_on: [player_heat_contract.md, box_score_storage_contract.md, api_surface.md, auth_tenancy_contract.md, ../safety/tenancy_policy.md]
status: implemented
last_updated: 2026-09-21
owners: [engineering]
---

# Query boundary

Two authenticated, owned routes: `GET /api/leagues/{id}/performance-pools` and
`GET /api/leagues/{id}/performance`. Authorize the league before validating query
values or reading shared observations. Another user's league returns 404; anonymous
requests return 401. Points leagues only; category requests return 409.

Pools describe stored regular-season snapshots grouped by season/source, with game
count, latest game date and latest retrieval time. Select the latest correction
before phase filtering. Pool existence does not certify a complete NBA season.

Performance requires explicit `seasonEndYear`, canonical `source`, ISO `throughDate`
and `view` (`best`, `hot`, `all`). Dates cannot exceed today's UTC date and seasons
use the existing NBA season bounds. Standard page/limit validation applies. Read
only current corrected regular-season snapshots, then score under the league's
currently saved rules using `PlayerHeatCalculator`. No persisted ranking cache,
season-stat fallback, provider merging, automatic prior-season fallback or draft
weight change. Repeated GETs have no publication side effects.

`best` orders qualified current averages descending; `hot` orders positive absolute
points-above-baseline descending; `all` includes observed but insufficient players
in deterministic player-ID order. Ties use player ID. Pagination applies after
ranking. Return the exact scoring-rule snapshot (canonical stat name and points per unit),
policy/model version, observed/qualified counts, latest appearance,
latest retrieval time and paged `PlayerHeatResult` evidence. Zero eligible games
returns a successful empty result with null freshness dates, never fictional zeros.

# Presentation

React offers explicit pool/date selection and separate best/above-baseline/all
views below the draft player table. It does not change the draft board's row or
interaction budget. The date and source displayed come from the response. Each
player exposes current/recent/comparison values, sample counts, dates and source
provenance; insufficient results remain labelled. Window lengths come from server
policy, not a second implementation in JavaScript. Formatting never scores games.

All results are labelled recorded performance, with completeness/freshness
unverified. Dates and retrieval times are visible. No live-streak claim, arbitrary
freshness threshold, forecast or injury/sustainability inference is introduced.
A historical game-date cutoff is not an as-known-at backtest. Changed controls do
not relabel older results; obsolete requests are cancelled. Retry is explicit.

# Verification

HP-01 persisted games → HTTP ranking/evidence and scoring edits; HP-02 empty,
insufficient, DNP and dated selection; HP-03 request/category/paging validation;
HP-04 ownership and anonymous denial; HP-05 Playwright selection, separate views,
evidence, missing-data/error recovery, accessibility and narrow layouts.
