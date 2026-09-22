---
type: test_matrix
title: Test Matrix — Recorded Performance
description: API, tenancy and browser evidence for recorded league-point performance.
tags: [tests, api, heat, ui]
source_paths: [src/FantasyBasketball.Application/Trends, src/FantasyBasketball.Web/src/performance.tsx]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api/PlayerPerformanceHttpTests.cs, tests/FantasyBasketball.IntegrationTests/Auth/RouteIsolationTests.cs, tests/FantasyBasketball.IntegrationTests/Auth/AuthHttpTests.cs, src/FantasyBasketball.Web/tests/workspace.mjs]
depends_on: [required_gates.md, ../contracts/player_performance_api_contract.md]
status: implemented
last_updated: 2026-09-21
owners: [engineering]
---

| ID | Required case | Expected | Required |
|---|---|---|---|
| `HP-01` | Persisted games, separate rankings, evidence, scoring edit | Exact league scores, disjoint baseline, reranking on next read | ✅ |
| `HP-02` | Empty, insufficient history, DNP, date cutoff, source isolation | Honest nulls/counts and no invented samples | ✅ |
| `HP-03` | Invalid queries, category league, paging | 400/409 envelopes; bounded deterministic pages | ✅ |
| `HP-04` | Cross-user and anonymous requests | 404 without identifiers; 401 anonymous | ✅ |
| `HP-05` | Real-cookie Playwright performance journey | Selection, modes, evidence, empty/error retry, axe and mobile overflow checks | ✅ |
