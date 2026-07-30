---
type: component
title: Background Workers
description: Hosted services for recurring data refresh, with per-source cadences rather than one shared timer.
tags: [component, workers, hosting]
source_paths: [src/FantasyBasketball.Infrastructure/Workers]
test_paths: [tests/FantasyBasketball.IntegrationTests/Workers]
depends_on: [ingestion_pipeline.md, ../safety/scraping_policy.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Each source refreshes on its own cadence, from configuration.
  - A worker failure never takes down the host.
---

# Responsibility

Owns recurring refresh. Different information goes stale at different rates, and
refreshing everything on one timer is both wasteful and a fast route to a rate
limit ban.

# Workers and cadences

Defaults, all configurable:

| Worker | Cadence | Why |
|---|---|---|
| `ScheduleRefreshWorker` | Daily | Schedules change rarely and predictably |
| `StatRefreshWorker` | Daily, off-peak | Season pages change at most once per day |
| `AdpRefreshWorker` | Daily during preseason, off otherwise | ADP moves daily while drafting, never after |

No news worker in the MVP — there is no news provider. No draft worker: the
draft board recomputes on demand, driven by picks, not by a timer.

# Design

- `BackgroundService` per worker. No Hangfire, no Quartz — three timers do not
  justify a scheduling framework or its storage.
- Each worker resolves a scoped service provider per run; a hosted service is a
  singleton and capturing a scoped `DbContext` in one is the classic .NET
  lifetime bug.
- Every run goes through the ingestion pipeline, so rate limits and caching apply
  identically to scheduled and user-triggered imports.
- Startup is staggered so workers do not all fire at boot and burst the limiter.

# Invariants

- **A worker exception is caught, logged, recorded as a failed
  `DataImportRun`, and the worker continues.** An unhandled exception in a
  `BackgroundService` stops the service silently, and the app then looks healthy
  while going stale. *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row W-01 makes a run throw and asserts the next run still executes.*
- **Workers honor the host's shutdown token** and stop promptly. *Check: row W-02.*
- **No worker bypasses the rate limiter.** *Check: row S-11 covers all callers.*
- **Cadences come from configuration**, never hardcoded intervals. *Check: the
  canonical-value grep.*
- **A scoped provider is created per run.** *Check: row W-03 asserts a fresh
  `DbContext` per iteration.*

# Change procedure

Adding a worker: the service, its cadence option, its registration, and its
failure test — one commit.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md),
rows W-01 through W-03.

# Implementation evidence

`ScheduleRefreshWorker`, `StatRefreshWorker`, and `AdpRefreshWorker` are separate
hosted services over the same queued ingestion pipeline. Their startup delays
and cadences bind from `RefreshWorkers`; schedule windows are configurable,
the season rolls over in July, and ADP is inactive outside its configured
preseason month range. Startup is staggered by default. The queue creates an
async scope for every run, so provider, transaction, and `DbContext` lifetimes
are never captured by a singleton. W-01 through W-03 prove a failed run is
recorded, two subsequent runs complete, every iteration receives fresh scoped
dependencies, all three workers are registered, configured cadence values bind,
and host cancellation stops a worker waiting in its startup delay.
