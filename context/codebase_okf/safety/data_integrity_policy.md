---
type: safety_policy
title: Data Integrity Policy
description: Immutable baselines, human-gated verification, required provenance, and the rules that keep estimates from becoming facts.
tags: [safety, integrity, projections]
source_paths: [src/FantasyBasketball.Domain/Projections, src/FantasyBasketball.Domain/Context]
test_paths: [tests/FantasyBasketball.Domain.Tests/Projections]
depends_on: [../contracts/projection_pipeline_contract.md, ../contracts/provenance_contract.md, ../contracts/context_event_catalog.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - The baseline of any projection can be recovered at any time.
  - No inferred or scraped claim is ever presented as fact.
---

# Responsibility

Owns the rules that keep this application's numbers honest. The product's whole
claim is that its recommendations are auditable; every rule here exists because
breaking it makes an unauditable number look authoritative.

# Rules

## 1. Baselines are immutable

`ObservedStats` and `BaselineProjection` are written once and never updated.
Context produces a **new** `AdjustedProjection` referencing the baseline by id.

A new import or model version writes a new baseline row; it does not edit the
old one. This is what makes *"why did this number change last Tuesday?"*
answerable.

## 2. Estimates are labelled as estimates

`PlayerContextImpact` deltas are **estimates**, not measurements, and every
surface that shows them says so. An adjusted projection always displays its
decomposition — baseline, adjustment, final — never the final number alone.
That is requirement R8, and it is a safety rule as much as a UI one.

## 3. Nothing becomes ground truth without a human

A `ContextEvent` reaches `Verified` only through an explicit user action at the
verification endpoint. No parser, importer, scheduled job, or future model may
write that state. Scraped commentary that silently became fact would poison
every downstream projection with no trace.

## 4. Everything imported carries provenance

Non-negotiable, per [provenance_contract](../contracts/provenance_contract.md).
A row whose origin is unknown cannot be audited and therefore cannot be trusted.

## 5. Degrade loudly

When a source is stale or failed, confidence drops, a `DataQuality` evidence item
appears, and the health view shows it. The application must never present
degraded output as if it were fresh. Silence is the failure mode being
prevented.

## 6. The user's override wins

Manual entry beats every automated source. A user correcting a roster, a stat, a
pick, or a context impact is authoritative, and their override is recorded with
`Source = manual` — not silently merged into the imported value.

# Invariants

- **No update path exists to `BaselineProjection` or `ObservedStats`.** Enforced
  by repository shape, not discipline. *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) rows
  A-04 and A-05.*
- **Applying then removing a context event leaves the baseline byte-identical.**
  *Check: [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md)
  row P-05.*
- **`VerificationState.Verified` is assigned in exactly one place.** *Check: row
  C-01, plus a grep asserting a single assignment site.*
- **An `AdjustedProjection` always names the baseline it came from and the events
  applied.** A null `BaselineProjectionId` is impossible. *Check: row P-09.*
- **Every imported row has non-null provenance.** *Check: row I-07 and the
  `NOT NULL` schema assertion.*
- **A failed source lowers confidence rather than silently reusing stale data as
  fresh.** *Check: row E-04.*

# Change procedure

These rules are `stable_contract`. Weakening one requires explicit user approval
and an entry in [assumptions](../assumptions.md) recording what was traded away
and why.

# Verification

Rows A-04, A-05, P-05, P-09, C-01, I-07, E-04.

# Implementation evidence

`ObservedStats` and `BaselineProjection` now have append-only repository
surfaces, and `FantasyDbContext` rejects tracked modification or deletion of
either row type. A baseline stores its immutable source-independent math while
the observed row references the exact provenance-carrying season stat line.
Context verification, adjustment decomposition, and degraded-confidence
evidence remain, so the broader policy stays `partial`.
