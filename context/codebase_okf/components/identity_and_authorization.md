---
type: component
title: Identity and Authorization
description: ASP.NET Identity wiring, the query-filter layer, the ownership retrofit migration, and the reflection-driven isolation sweep.
tags: [component, auth, tenancy]
source_paths: [src/FantasyBasketball.Infrastructure/Identity, src/FantasyBasketball.Api/Endpoints/AccountEndpoints.cs, src/FantasyBasketball.Web/src/Workspace.tsx]
test_paths: [tests/FantasyBasketball.IntegrationTests/Auth]
depends_on: [../contracts/auth_tenancy_contract.md, ../safety/tenancy_policy.md]
status: implemented
last_updated: 2026-09-23
owners: [engineering]
risk_level: high
done_criteria:
  - Adding an owned entity without a query filter fails a test, not a review.
  - Existing single-user data migrates to the first owner without loss.
---

# Responsibility

Owns accounts and the enforcement of ownership. Schema, the owned/shared split, and
the defaults are owned by
[auth_tenancy_contract](../contracts/auth_tenancy_contract.md); the
non-negotiables by [tenancy_policy](../safety/tenancy_policy.md).

# The retrofit

This epic adds ownership to a schema that did not have it, which is the risky part:

1. Add `OwnerId` as **nullable**, migrate, deploy.
2. On first registration, claim every existing null-owner row for that user — the
   pre-auth instance was single-user by construction, so this is correct and is the
   only moment it is correct.
3. A second migration makes `OwnerId` non-nullable.

Doing it in one step would either lose the existing league or require a placeholder
user. The two-step is slower to write and the only version that preserves data.

# Enforcement

- **Query filters** are applied in `OnModelCreating` by iterating entity types that
  implement `IOwnedResource`, not by a hand-written list. A new owned entity gets a
  filter by implementing the marker — the mechanism cannot be forgotten, only
  declined.
- **Endpoint authorization** re-checks the resolved resource's owner. Two layers,
  because a filter is bypassable.
- `IUserContext` resolves from the request principal. In a worker scope it throws on
  access rather than returning a default — a silent default user is how cross-tenant
  writes happen.

# The sweep is the real test

`test_matrix_auth_tenancy` row U-01 **enumerates owned routes by reflection** and
runs each as a second user against the first user's resources, asserting `404`. It
does not read a list of routes.

This matters: a hand-maintained list is exactly as complete as the last person who
remembered to update it, and the failure mode of forgetting is a cross-tenant leak.
An enumerating sweep fails the day a new owned endpoint appears without ownership
wiring.

# Invariants

- **The sweep enumerates rather than lists.** *Check:
  [test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md) rows U-01 and
  U-02, where U-02 adds a deliberately unfiltered entity in a test fixture and
  asserts the sweep fails.*
- **Query filters are applied by marker interface.** *Check: row U-15 asserts every
  `IOwnedResource` type has a filter.*
- **`IUserContext` throws outside a request.** *Check: row U-03.*
- **The retrofit preserves existing data.** *Check: row U-16 seeds pre-auth rows,
  registers, and asserts every row is claimed.*
- **Cookie flags, HTTPS, and lockout defaults hold.** *Check: rows U-08 and U-09.*
- **Exactly one instance owner; closed registration returns `404`.** *Check: rows
  U-06 and U-07.*
- **Identity shares the one `DbContext` and migration history.** *Check: row U-17.*

# Change procedure

Adding an owned entity: implement `IOwnedResource`, migrate, and add the ownership
row in the contract — the filter and the sweep follow automatically. **Never add an
owned entity without the marker.**

# Verification

[test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md), rows U-01 through
U-17.

# Current evidence

Identity uses the existing PostgreSQL context and migration history, with
snake-case Identity tables and application-generated GUID user ids. The two-step
retrofit preserves the nullable claim boundary and makes required ownership
non-null only after first-registration claiming. Marker-driven query filters,
endpoint authorization, throwing worker context, account pages, portable account
archives, and cascade deletion are implemented. U-01 reflects over all 10 owned
routes and U-02 proves the sweep detects a planted leak; U-01 through U-17 pass.

## Blazor retired — 2026-09-23

Sign-in and registration are the React form posting to `/api/account/*`
(cookie + antiforgery unchanged). The Razor account pages and their HTML
form-post endpoints (`/account/*/submit`, `/account/active-league/submit`) were
removed with Blazor; unauthenticated page requests now redirect to `/app`.
