---
type: component
title: Persistence
description: EF Core DbContext, entity configurations, migrations, and repository implementations.
tags: [component, persistence, efcore]
source_paths: [src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests/Persistence]
depends_on: [../contracts/persistence_contract.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Migrations apply from empty to a Testcontainers Postgres in CI.
  - Append-only tables have no update path in any repository.
---

# Responsibility

Owns the EF Core implementation. Schema rules, conventions, indexes, and
migration policy are owned by
[persistence_contract](../contracts/persistence_contract.md).

# Design

- One `FantasyDbContext`. Configuration lives in `IEntityTypeConfiguration<T>`
  classes, one per entity — not in a thousand-line `OnModelCreating`.
- Repositories implement the Application interfaces and return domain types.
  They never leak `IQueryable` outward; a caller that can compose a query can
  bypass every rule this layer enforces.
- Append-only repositories expose `AddAsync` and reads, and no update method at
  all. The guarantee is the shape of the interface, not a code review.
- `AsNoTracking` for reads by default; tracking only where a write follows.

# Invariants

- **No `EnsureCreated`.** Schema comes from migrations only. *Check: a grep in
  the gate; `EnsureCreated` silently diverges from the migration history and
  makes production unreproducible.*
- **Append-only tables have no update surface.** *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) rows
  A-04 and A-05.*
- **Migrations apply from empty in tests**, so a broken migration fails CI, not
  a developer's afternoon. *Check: row A-16.*
- **Enums persist as strings.** *Check: row A-08.*
- **Repositories return domain types, never entities-as-DTOs or `IQueryable`.**
  *Check: row A-17 reflects over the interfaces.*
- **Integration tests use a throwaway container per fixture**, never a developer
  database. *Check: row A-06.*
- **Cascade behavior matches the contract**: leagues cascade to draft data;
  players never cascade to stat history. *Check: row A-09.*

# Change procedure

Adding an entity: configuration class, migration, index if hot, repository, and
the contract's entity list — one commit.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows
A-04 through A-09 and A-16, A-17.

# Implementation evidence

The forward `IdentityResolution` and `ImportRuns` migrations add
`PendingIdentityMatch`, the one-provider-link-per-player constraint, and
append-only `DataImportRun` storage. The identity resolver's player/link/pending
write and its successful run record commit in one EF transaction. Persistence
also round-trips imported NBA teams and games with source provenance through
the forward `ScheduleSource` migration. The forward `AdpEntries` migration and
repository add provenance-complete, player-linked market data and latest-value
reads. It remains `partial` because later projection, context, and
recommendation entities in the contract are not implemented yet. The
`ProjectionRecords` migration and repository now add immutable
`ObservedStats` and full `BaselineProjection` persistence; adjusted
projections and fantasy values remain.
