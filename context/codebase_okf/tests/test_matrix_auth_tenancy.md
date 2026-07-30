---
type: test_matrix
title: Test Matrix — Auth and Tenancy
description: Required cases for cross-user isolation, the enumerating route sweep, the ownership retrofit, and Identity defaults.
tags: [tests, auth, tenancy, security, matrix]
source_paths: [src/FantasyBasketball.Infrastructure/Identity]
test_paths: [tests/FantasyBasketball.IntegrationTests/Auth]
depends_on: [required_gates.md, ../contracts/auth_tenancy_contract.md, ../safety/tenancy_policy.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Gates R20. Row `U-01` is the most important test in the repository: it is the one
standing between two users' league data.

# Isolation (`U-01`–`U-05`, `U-11`, `U-12`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `U-01` | **Every owned route, enumerated by reflection**, called as user B against user A's resources | `404` on all of them. Never `403`, never `200` | ✅ |
| `U-02` | A deliberately unfiltered owned entity added in a fixture | The sweep **fails** — proving it enumerates rather than reading a list | ✅ |
| `U-03` | Owned-entity repository call with no user context | Throws; never resolves to a default user | ✅ |
| `U-04` | Two users import overlapping players | One shared row per player; reference data is not duplicated | ✅ |
| `U-05` | `AdjustedProjection` with and without an owner | Personal invisible to the other user; global visible to both | ✅ |
| `U-11` | Each background worker under a throwing user context | Touches only shared tables | ✅ |
| `U-12` | Sweep response bodies | Contain none of the other user's identifiers | ✅ |

# Registration and ownership (`U-06`, `U-07`, `U-16`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `U-06` | First registration on an empty instance | Becomes the sole instance owner | ✅ |
| `U-07` | Second registration, closed instance | `404` from the register endpoint — not a form that rejects | ✅ |
| `U-16` | Pre-auth rows, then first registration | Every null-owner row claimed; no data lost | ✅ |

# Identity defaults (`U-08`–`U-10`, `U-17`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `U-08` | Auth cookie | `HttpOnly`, `Secure`, `SameSite=Lax`; HTTPS enforced outside Development | ✅ |
| `U-09` | Five failed logins | Locked out for 15 minutes | ✅ |
| `U-10` | Captured logs across an auth flow | No password, hash, or cookie value present | ✅ |
| `U-17` | Identity tables | In the one `DbContext` and the one migration history | ✅ |

# Filters, deletion, export (`U-13`–`U-15`)

| ID | Case | Expected | Required |
|---|---|---|---|
| `U-13` | Delete a user | All owned rows gone; no shared row and no other user's row touched | ✅ |
| `U-14` | Export then import into an empty account | Equivalent object graph — export round-trips | ✅ |
| `U-15` | Every `IOwnedResource` type | Has a query filter applied | ✅ |

# Why enumeration, not a list

A hand-maintained route list is exactly as complete as the last person who
remembered to update it, and the cost of forgetting is a cross-tenant leak. `U-01`
discovers routes; `U-02` proves the discovery works by planting a hole and asserting
the sweep finds it. A sweep that cannot fail is not a test.

# Verification

`dotnet test --filter Auth`, inside the full gate, with Docker running.
