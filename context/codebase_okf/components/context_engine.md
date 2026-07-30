---
type: component
title: Context Engine
description: Manages context events and their impacts, and applies them to a baseline to produce an adjusted projection.
tags: [component, context, projections]
source_paths: [src/FantasyBasketball.Domain/Context, src/FantasyBasketball.Application/Context, src/FantasyBasketball.Application/Projections/ContextApplier.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Context]
depends_on: [../contracts/context_event_catalog.md, ../safety/data_integrity_policy.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - A user can create, verify, reject, and override events and their impacts.
  - Applying and removing an event leaves the baseline untouched.
---

# Responsibility

Owns the product's differentiator: converting basketball context into structured
adjustments that modify projections *without* destroying the statistical
baseline. The vocabulary is owned by
[context_event_catalog](../contracts/context_event_catalog.md) and the
application math by
[projection_pipeline_contract](../contracts/projection_pipeline_contract.md).

# Design

- `ContextEventService` — create, verify, reject, override, expire. The
  verification endpoint is the single writer of `Verified`.
- `ContextApplier` — pure function: `(BaselineProjection, events) →
  AdjustedProjection`. No I/O, no repository, so it is trivially testable and
  cannot accidentally persist a mutation.
- Event selection happens at computation time: applicable events are those
  where `EffectiveFrom ≤ now` and (`ExpectedExpiration` is null or `> now`) and
  `VerificationState != Rejected`. Expiry is a query predicate, not a cleanup
  job — a job that fails would silently leave stale events applying.

# MVP scope

Events are **user-created only**. There is no news scraper and no LLM proposer
in the MVP; both are post-MVP. The human-in-the-loop workflow is built now so
that when a proposer arrives, the review surface already exists and unverified
machine suggestions have somewhere to land that is not the projection itself.

# Invariants

- **`ContextApplier` is pure and takes no repository.** *Check: its signature.*
- **The baseline is never mutated.** *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row P-05.*
- **Nothing reaches `Verified` outside the verification endpoint.** *Check: row
  C-01 plus a single-assignment-site grep.*
- **Proposed events apply at reduced confidence and are visibly marked;
  rejected events never apply and are retained.** *Check: rows C-02 and C-03.*
- **Expired events stop applying without a cleanup job.** *Check: rows C-04, P-08.*
- **Overrides replace defaults entirely and are recorded as overridden.**
  *Check: row C-05.*
- **Usage and shot volume never compound.** *Check: row P-06.*

# Change procedure

Adding an event type: catalog first (member + default deltas), then the enum,
the review UI picker, and a defaults test — one commit.

# Verification

[test_matrix_projection_draft](../tests/test_matrix_projection_draft.md), rows
C-01 through C-07 and P-05 through P-09.

# Implementation evidence

`ContextEventService` creates proposed events and provides audited human verify,
reject, expire, and impact-override actions over an Application repository
contract. `ContextApplier` is pure Domain code: it selects effective,
non-expired, non-rejected events at computation time, clamps minutes and
aggregate multipliers, prevents usage/shot-volume compounding, recomputes final
stat components, and returns a new `AdjustedProjection` naming its immutable
baseline and applied event ids. Domain, Application, and PostgreSQL integration
tests cover every required context and adjusted-projection row.
