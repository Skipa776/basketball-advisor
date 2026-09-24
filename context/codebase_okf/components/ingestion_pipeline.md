---
type: component
title: Ingestion Pipeline
description: HTTP client setup, resilience, per-host rate limiting, response caching, import-run recording, and degraded operation.
tags: [component, ingestion, http]
source_paths: [src/FantasyBasketball.Infrastructure/Http, src/FantasyBasketball.Infrastructure/Import]
test_paths: [tests/FantasyBasketball.IntegrationTests/Ingestion]
depends_on: [../safety/scraping_policy.md, ../contracts/provenance_contract.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: high
done_criteria:
  - Every outbound request passes through the rate limiter and the cache.
  - A failed import records a failed run and leaves prior data intact.
---

# Responsibility

Owns every outbound HTTP request. One pipeline, shared by API providers and
scrapers alike, so rate limiting and caching cannot be bypassed by adding a new
adapter. Rate ceilings and the host allowlist are owned by
[scraping_policy](../safety/scraping_policy.md).

# Design

Ordered handler chain, per named `HttpClient` (one per host):

```text
request → cache lookup → resilience (retry + backoff + circuit breaker)
        → per-host rate limiter → send → hash + record → cache store
```

The limiter is inside the resilience handler so **every retry attempt** acquires
a permit. Putting resilience inside the limiter would throttle only the first
attempt and let its retries exceed the safety ceiling.

- **Rate limiting** uses `System.Threading.RateLimiting` from the shared
  framework, keyed by host, registered as a singleton. Per-instance limiters
  would let a second client double the real rate.
- **Resilience** is `Microsoft.Extensions.Http.Resilience`: exponential backoff
  with jitter, retry budget, circuit breaker. Not a hand-rolled retry loop.
- **Caching** is `IMemoryCache` with per-source freshness windows: schedules and
  season stats daily, ADP daily, player directory daily.
- **`DataImportRun`** records source, started/finished, rows written, pending
  identity matches, and failure detail for every import — successful or not.

# Degraded operation

A source failing is normal, not exceptional. On failure the pipeline records a
failed run, leaves previously imported data untouched, lowers confidence
downstream, and surfaces staleness through the health endpoint. It never throws
out of a user request and never serves stale data as if it were fresh — see
[data_integrity_policy](../safety/data_integrity_policy.md).

# Invariants

- **No `new HttpClient(...)` anywhere.** Every client comes from
  `IHttpClientFactory`. *Check: the `new HttpClient\(` forbidden-pattern scan.*
- **The rate limiter is process-wide per host and is asserted by test**, not
  assumed. *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row S-11.*
- **A cache hit inside the freshness window issues no request.** *Check: row I-12.*
- **Every import writes a `DataImportRun`, including failures.** *Check: row I-06.*
- **A failed import leaves prior data intact** — no partial overwrite, no
  truncate-then-load. *Check: row I-13.*
- **Cancellation stops the import without a partial commit.** *Check: row I-09.*
- **Every persisted row carries provenance with the raw-fragment hash.**
  *Check: rows I-07 and I-10.*
- **No test performs a real network request.** *Check: row S-12.*

# Change procedure

Adding a host: a named client with its own limiter and freshness window, its
entry in the scraping policy allowlist, and its adapter — one commit.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md),
rows I-06 through I-13 and S-11, S-12.

# Implementation evidence

The API host registers one named `IHttpClientFactory` client per canonical HTTP
source. Cache, singleton per-host token-bucket limiting, and the platform
standard resilience handler are ordered ahead of every send. Offline tests
prove cache hits make zero sends and 20 requests across two handlers share one
limiter. Real PostgreSQL tests prove failed and canceled imports roll back every
partial identity write; non-cancellation failures become immutable failed
`DataImportRun` rows. Health-driven confidence degradation and provider-level
I-06 coverage remain, so this component remains `partial`.

## Current teams — 2026-09-23

A player's `CurrentTeamId` was set when first seen and never changed, so traded
players kept their old team (the live data had James Harden on the Clippers after
his 2025-26 trade to Cleveland). The balldontlie player directory is now the
source of truth: `ImportPlayersService` saves the directory's team whenever it
differs, and `PlayerDirectoryRefreshWorker` re-imports the directory weekly (under
the `RefreshWorkers` master switch). Box-score and season-stat imports never
change a team. Evidence: `Directory_import_moves_a_traded_player_to_the_current_team`;
U11 runs the new worker with a throwing user context.
