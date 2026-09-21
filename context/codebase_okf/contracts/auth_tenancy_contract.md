---
type: contract
title: Auth and Tenancy Contract
description: Identity schema, the owned-versus-reference data split, two-layer isolation enforcement, registration policy, and cookie/lockout defaults.
tags: [contract, auth, tenancy, security]
source_paths: [src/FantasyBasketball.Infrastructure/Identity, src/FantasyBasketball.Domain/Accounts]
test_paths: [tests/FantasyBasketball.IntegrationTests/Auth]
depends_on: [persistence_contract.md, ../safety/tenancy_policy.md]
status: implemented
last_updated: 2026-09-21
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Cross-user access is impossible through any endpoint or repository.
  - Reference data is shared, not duplicated per user.
  - A fresh instance is claimable by exactly one owner and then closed by default.
---

# Responsibility

Owns accounts and data ownership. Full ASP.NET Identity was chosen deliberately
([assumptions](../assumptions.md)) — which means ownership has to be *real*.
Identity guarding a single-tenant database would be ceremony; this contract makes
the data model actually multi-tenant so the auth is load-bearing.

# The split that is the whole design

| Owned — scoped to a user | Shared — global reference data |
|---|---|
| `FantasyLeague`, `ScoringRule`, `RosterSlot` | `Player`, `NbaTeam`, `ExternalPlayerIdentity` |
| `DraftSession`, `DraftPick` | `SeasonStatLine`, `RollingWindow`, `NbaGame`, `AdpEntry` |
| `ContextEvent`, `PlayerContextImpact` | `ObservedStats`, `BaselineProjection` |
| `Recommendation`, `RecommendationEvidence` | `DataImportRun`, `PendingIdentityMatch` |
| `FantasyValue` (league-scoped) | |

**Reference data is shared because it is a fact about the NBA, not about a user.**
Duplicating it per user would multiply the scrape budget by the user count — and
the scrape budget is 6 requests per minute
([scraping_policy](../safety/scraping_policy.md)). Ten users each triggering their
own Basketball-Reference import is a ban, not a feature. Shared reference data is
what makes a multi-user instance possible at all.

`AdjustedProjection` is shared when it applies only verified global events, and
owned when it applies a user's own context events. It carries a nullable
`OwnerId`: null means global, set means personal. This is the one entity where the
split is per-row, and it is the row most likely to be got wrong.

`BoxScoreSnapshotRow` and `PlayerGameStatRow` are also explicitly shared NBA
reference data: completed game observations and their provenance, used by the
authorized per-game/heat implementation. They contain no league, roster, account
or private provider data. No previously owned entity changes classification.

# Enforcement — two layers, on purpose

1. **EF Core global query filter** on every owned entity, keyed to
   `IUserContext.CurrentUser`. Catches every ordinary query by construction.
2. **Explicit authorization at each endpoint**, checking the resolved resource's
   `OwnerId`.

Two layers because layer 1 is bypassable — `IgnoreQueryFilters()` exists, and a
raw SQL query or a `DbContext` resolved outside a request scope has no ambient
user. Background workers run with **no user context** and may touch only shared
tables; requesting an owned entity from a worker throws.

# Not-found, not forbidden

A request for another user's resource returns **`404 not_found`**, never `403`.
A `403` confirms the resource exists, which leaks league ids across accounts. The
only place `403` appears is a resource the user owns but may not act on in its
current state — and lifecycle conflicts are `409`
([api_surface](api_surface.md)).

# Identity schema

```text
FantasyUser : IdentityUser<Guid>
  DisplayName
  CreatedAt
  IsInstanceOwner       # exactly one, set on first registration
```

Identity tables live in the same `FantasyDbContext` and the same migration
history — a second context and a second connection buy nothing for a
single-database self-hosted app.

Roles: **`Owner` and nothing else.** No admin panel, no role editor, no
permission matrix. The owner claims the instance and can toggle registration;
that is the entire authorization surface, and adding more would be inventing
requirements.

# Registration policy

```text
Instance has zero users  → registration open; first registrant becomes Owner
Instance has an Owner    → registration closed unless Auth:OpenRegistration = true
```

This is what makes a self-hosted instance safe to expose: the window in which
anyone can create an account closes as soon as the operator finishes setup.
A closed-registration instance returns `404` from the register endpoint, not a
form that rejects.

# Security defaults

| Setting | Value |
|---|---|
| Auth scheme | Cookie |
| Cookie flags | `HttpOnly`, `Secure`, `SameSite=Lax` |
| HTTPS | Required outside `Development`; HSTS on |
| Password | Minimum 12 characters, no composition rules |
| Lockout | 5 failures, 15-minute lockout |
| Anti-forgery | On for every state-changing form and endpoint |
| Rate limit | Login and register endpoints limited per IP |

Length over composition rules is deliberate — composition rules produce
`Password1!` and nothing else.

# Invariants

- **No endpoint returns another user's data.** *Check:
  [test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md) row U-01
  sweeps every owned route as user B against user A's resources and asserts
  `404` on all of them.*
- **The sweep is exhaustive by construction** — it enumerates routes by
  reflection, so a new owned endpoint added without a test fails the sweep.
  *Check: row U-02.*
- **A repository call for an owned entity with no user context throws.** *Check: row U-03.*
- **`IgnoreQueryFilters` on an owned entity appears nowhere in `src/`.** *Check: a
  gate grep.*
- **Reference data is readable by every user and duplicated for none.** *Check:
  row U-04 asserts one row per player across two users' imports.*
- **`AdjustedProjection` respects its nullable owner** — a personal adjustment is
  invisible to another user, a global one is visible to both. *Check: row U-05.*
- **Exactly one instance owner exists**, and the second registrant on a closed
  instance gets `404`. *Check: rows U-06 and U-07.*
- **Cookies carry all three flags and HTTPS is enforced outside Development.**
  *Check: row U-08.*
- **Lockout triggers at 5 failures.** *Check: row U-09.*
- **Password hashes and cookies never appear in logs.** *Check: row U-10.*

# Change procedure

Adding an owned entity: `OwnerId` column, a migration, the query filter, the
ownership table above, and a row in the U-01 sweep — one commit. **Adding an owned
entity without the filter is the highest-severity mistake available in this
codebase**, which is why the sweep enumerates rather than lists.

# Verification

[test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md), rows U-01
through U-10.

# Current evidence

`FantasyUser : IdentityUser<Guid>` and the sole `Owner` role share the existing
`FantasyDbContext` and migration history. The first migration adds nullable
ownership, first registration atomically claims pre-auth rows, and the second
migration makes required owners non-null. Marker-driven query filters and
explicit endpoint checks enforce the exact owned/shared split, while null-owned
adjusted projections remain global. Closed registration, cookie security,
lockout, anti-forgery, HTTPS, account export/import, and deletion are covered by
U-01 through U-17.
