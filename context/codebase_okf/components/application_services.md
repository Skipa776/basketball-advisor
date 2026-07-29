---
type: component
title: Application Services
description: Use-case orchestration, repository abstractions, async and cancellation rules, and the no-ceremony rule.
tags: [component, application, use-cases]
source_paths: [src/FantasyBasketball.Application]
test_paths: [tests/FantasyBasketball.Application.Tests]
depends_on: [../contracts/provider_contracts.md, ../contracts/api_surface.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Every service is constructor-injected and unit-testable with hand-written fakes.
  - No service depends on a concrete Infrastructure type.
---

# Responsibility

Owns the use cases — the verbs the product performs: create a league, import
players, project a pool, run a draft, review context. Services orchestrate; the
domain decides. A service that contains a formula is a bug: the formula belongs
in the domain behind a contract.

# Design

- **Repository interfaces are declared here, implemented in Infrastructure.**
  The dependency arrow points inward.
- **Constructor injection only.** No service locator, no static access to
  configuration, no `IServiceProvider` passed around.
- **Direct calls, no mediator.** With this many use cases, a message bus adds
  indirection and a licence problem and buys nothing —
  [`stack_config.toml`](../../../stack_config.toml) forbids MediatR.
- **Hand-written fakes in tests**, not a mocking framework. The interfaces are
  small enough that a fake is shorter than the mock setup, and it stays readable
  at 3am.

# Invariants

- **Async all the way down.** No `.Result`, no `.Wait()`, no
  `GetAwaiter().GetResult()`. *Check: the `\.Result|\.Wait\(\)`
  forbidden-pattern scan.*
- **Every async method takes a `CancellationToken` as its last parameter and
  passes it to every await.** A cancelled import must not half-commit. *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row I-09.*
- **No Infrastructure types.** No `DbContext`, no `HttpClient`, no provider
  class referenced by name. *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-03.*
- **Errors are handled explicitly at every level; nothing is silently
  swallowed.** *Check: the empty-catch forbidden-pattern scan.*
- **A failing source degrades the result** — partial data plus lowered
  confidence — rather than throwing out of the use case. *Check: row I-06.*
- **Application tests run with no database and no network.** *Check: the test
  project's references.*

# Change procedure

Adding a use case: the service, its repository interface if new, unit tests with
fakes, and an endpoint if it is user-reachable — one commit.

# Verification

`test_matrix_api_persistence.md` row A-03, `test_matrix_ingestion_scrapers.md`
rows I-06 and I-09, plus per-service unit tests.
