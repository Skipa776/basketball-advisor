---
type: component
title: Domain Model
description: The pure domain project — its types, its validity-by-construction rule, and the source-independence boundary it must never cross.
tags: [component, domain, architecture]
source_paths: [src/FantasyBasketball.Domain]
test_paths: [tests/FantasyBasketball.Domain.Tests]
depends_on: [../contracts/stat_vocabulary.md, ../contracts/scoring_rules_catalog.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - The Domain project has zero package references beyond the framework.
  - Every domain type is valid on construction or cannot be constructed.
---

# Responsibility

Owns the business concepts: players, leagues, stats, projections, context,
draft, recommendations. Type shapes are in
[`ARCHITECTURE.md`](../../../ARCHITECTURE.md).

This project is where the project's most important architectural rule lives:
**the domain never knows whether information came from ESPN, Yahoo, Sleeper, an
HTML scraper, a CSV, or a human.** Everything external translates into these
types at the boundary.

# Invariants

- **Zero external dependencies.** No EF Core, no `HttpClient`, no JSON
  attributes, no provider names, no `DbContext`. If a domain type needs an
  attribute to persist, the persistence layer configures it instead. *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-01
  reflects over the Domain assembly and asserts its referenced assemblies are
  framework-only.*
- **Valid by construction.** A domain type either cannot be built in an invalid
  state or throws in its constructor. There is no `IsValid()` to forget to call,
  and boundary validation ([api_surface](../contracts/api_surface.md)) exists to
  produce good error messages, not to protect the domain. *Check: row A-02.*
- **Records and value objects by default**, mutable classes only where identity
  and lifecycle demand it (`DraftSession`, `FantasyLeague`). Immutability is why
  a projection can be snapshotted and compared safely. *Check: prose plus review.*
- **Ids are wrapper types** (`PlayerId`, `NbaTeamId`), not raw `Guid`s, so a
  team id cannot be passed where a player id belongs. *Check: compile-time.*
- **No `DateTime.Now`, no ambient clock.** Time arrives as a parameter or an
  injected `TimeProvider`. *Check: the forbidden-pattern scan.*
- **Domain tests need no database, no network, and no I/O.** *Check: the Domain
  test project references no Testcontainers and no HTTP package.*

# Change procedure

Adding a domain concept: shape in `ARCHITECTURE.md`, the type here, its owning
contract if it introduces canonical values, and unit tests — one commit.

# Verification

`test_matrix_api_persistence.md` rows A-01 and A-02, plus each engine's own
test matrix.
