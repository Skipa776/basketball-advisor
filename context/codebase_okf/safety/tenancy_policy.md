---
type: safety_policy
title: Tenancy Policy
description: Default-deny data access, the not-found rule, worker isolation, the shared-reference-data exception, and user deletion and export.
tags: [safety, tenancy, authorization, privacy]
source_paths: [src/FantasyBasketball.Infrastructure/Identity, src/FantasyBasketball.Infrastructure/Persistence]
test_paths: [tests/FantasyBasketball.IntegrationTests/Auth]
depends_on: [../contracts/auth_tenancy_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - No request or worker can read or write another user's data.
  - The shared-data exception is enumerated, not implicit.
  - A user can export and delete everything they own.
---

# Responsibility

Owns the rules that keep users out of each other's data. Schema and mechanism are
owned by [auth_tenancy_contract](../contracts/auth_tenancy_contract.md); this file
owns the non-negotiables. **You may not weaken it to make a feature work.**

# 1. Default deny

Every owned entity is invisible without a matching `OwnerId`. New entities are
owned unless they appear on the shared list in the contract — the default for
anything unclassified is *owned*, because the failure mode of wrongly-owned data
is a confused user, and the failure mode of wrongly-shared data is a breach.

# 2. Not-found, never forbidden

Another user's resource returns `404`. A `403` confirms existence, which leaks
league and draft ids across accounts. There is no endpoint where distinguishing
"does not exist" from "not yours" is worth that.

# 3. Workers have no user

Background services run with **no user context** and may touch only shared tables.
Requesting an owned entity from a worker throws rather than resolving to some
arbitrary user. A worker that needs per-user work must be given an explicit user
id by an owned, authorized trigger — never by scanning.

# 4. The shared-data exception is enumerated

Reference data is shared, and the list of what is shared lives in the contract as
an explicit table. **Nothing is shared implicitly.** Sharing exists because the
scrape budget is 6 requests per minute and per-user duplication of NBA data would
be a ban, not because sharing is convenient.

# 5. No cross-tenant aggregates

Nothing derived from other users' owned data is displayed, ever — no "3 other
managers in your instance rostered him", no cross-league popularity, no
instance-wide leaderboards. Aggregates over a small self-hosted instance are
trivially de-anonymizable, and the feature value is near zero.

# 6. Deleting a user deletes what they own

Cascades to leagues, drafts, picks, their context events and impacts, their
personal adjusted projections, and their recommendations. It does **not** touch
reference data, and it does not touch another user's data even where the two
reference the same player. Deletion is complete and irreversible, and the
confirmation says so.

# 7. Export is a right, not a feature request

A user can export everything they own as JSON. This is cheap here — the data is
already a bounded object graph — and it is what makes a self-hosted instance
trustworthy: the operator can leave, and the data comes with them.

# Invariants

- **The cross-user route sweep passes for every owned endpoint**, and it enumerates
  routes by reflection so a new endpoint cannot skip it. *Check:
  [test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md) rows U-01 and U-02.*
- **Owned access with no user context throws.** *Check: row U-03.*
- **`IgnoreQueryFilters` appears nowhere in `src/`.** *Check: a gate grep — the one
  bypass that would silently defeat layer 1.*
- **Workers touch only shared tables.** *Check: row U-11 runs each worker under a
  context that throws on owned access.*
- **No response body contains another user's identifiers.** *Check: row U-12
  scans sweep responses for the other user's ids.*
- **User deletion removes all owned rows and no shared rows.** *Check: row U-13.*
- **Export round-trips**: exported JSON re-imports into an empty account and
  produces an equivalent object graph. *Check: row U-14.*

# Change procedure

`stable_contract`. Moving an entity from owned to shared requires explicit user
approval and a recorded rationale — it is a privacy decision, not a schema
decision. Adding an owned entity requires the query filter and a sweep row in the
same commit.

# Verification

[test_matrix_auth_tenancy](../tests/test_matrix_auth_tenancy.md), rows U-01,
U-02, U-03, U-11, U-12, U-13, U-14.
