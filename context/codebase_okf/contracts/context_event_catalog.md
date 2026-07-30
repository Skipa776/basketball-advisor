---
type: contract
title: Context Event Catalog
description: Event types, direction and magnitude scales, verification states, default impact deltas per event type, and expiration defaults.
tags: [contract, catalog, context]
source_paths: [src/FantasyBasketball.Domain/Context]
test_paths: [tests/FantasyBasketball.Domain.Tests/Context]
depends_on: [stat_vocabulary.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - ContextEventType members match this catalog exactly.
  - No event reaches Verified without an explicit human action.
  - Every event type has a default impact and a default expiration.
---

# Responsibility

Owns the structured vocabulary for basketball context — the differentiator of
this product. Statistics describe what happened; these describe what changed.
The application of these deltas to a projection is owned by
[projection_pipeline_contract](projection_pipeline_contract.md); this file owns
*what an event is*.

# Enums

```text
ContextEventType = Trade | Signing | Injury | ReturnFromInjury
                 | StartingLineupChange | BenchRoleChange | MinutesRestriction
                 | CoachStatement | FacilitatorChange | UsageChange
                 | PositionChange | RotationChange | RestRisk | DepthChartChange

ContextDirection  = Positive | Negative | Neutral
VerificationState = Proposed | Verified | Rejected
```

`Confidence` is shared with recommendations and owned by
[recommendation_evidence_contract](recommendation_evidence_contract.md).

# Scales

- **`Magnitude`** — `decimal` in `[0, 1]`. How big the change is, not which way;
  direction is separate. `0.25` is a noticeable rotation tweak, `1.0` is a
  franchise-altering trade.
- **`Confidence`** — how sure we are the event is real and will stick. A coach's
  offhand remark is `Speculative`; a completed trade is `High`.

Magnitude and confidence are deliberately separate: a rumored blockbuster is
high magnitude, low confidence.

# Default impacts by event type

Applied as `magnitude × direction × the deltas below`, then clamped per
[projection_pipeline_contract](projection_pipeline_contract.md). These are
**starting suggestions a human can override**, which is the entire point of the
human-in-the-loop design — they are never applied silently at full strength.

| Event type | Minutes Δ | Usage × | Assist × | Rebound × | Role risk Δ | Default expiry |
|---|---|---|---|---|---|---|
| `Trade` | ±3.0 | ±0.15 | ±0.15 | ±0.05 | +0.30 | 60 days |
| `Signing` | ±2.0 | ±0.10 | ±0.05 | ±0.05 | +0.20 | 60 days |
| `Injury` | −999 (out) | — | — | — | +0.50 | none — until `ReturnFromInjury` |
| `ReturnFromInjury` | +4.0 | ±0.05 | — | — | +0.25 | 30 days |
| `StartingLineupChange` | ±6.0 | ±0.10 | ±0.05 | ±0.05 | +0.15 | 30 days |
| `BenchRoleChange` | −6.0 | −0.10 | −0.05 | −0.05 | +0.25 | 30 days |
| `MinutesRestriction` | −8.0 | — | — | — | +0.35 | 14 days |
| `CoachStatement` | ±2.0 | ±0.05 | — | — | +0.10 | 14 days |
| `FacilitatorChange` | — | ±0.05 | ±0.25 | — | +0.20 | 60 days |
| `UsageChange` | — | ±0.20 | ±0.05 | — | +0.20 | 45 days |
| `PositionChange` | ±2.0 | ±0.05 | ±0.10 | ±0.10 | +0.25 | 45 days |
| `RotationChange` | ±4.0 | ±0.05 | — | — | +0.20 | 30 days |
| `RestRisk` | −2.0 | — | — | — | +0.15 | 30 days |
| `DepthChartChange` | ±3.0 | ±0.05 | ±0.05 | ±0.05 | +0.20 | 30 days |

`Injury` sets availability to zero rather than scaling minutes; `−999` is the
sentinel for "out", clamped to 0 minutes on application. Ratio-style columns are
multipliers on per-minute rates, expressed as the fractional change.

# Invariants

- **Nothing reaches `Verified` without an explicit human action.** No importer,
  parser, scraper, or future model may write `Verified`. This is the rule that
  stops scraped commentary from silently becoming ground truth. *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row
  C-01, plus a scan asserting `VerificationState.Verified` is assigned in exactly
  one place — the verification endpoint.*
- **`Proposed` events still affect projections**, at reduced confidence, and are
  visibly marked as unverified. Ignoring them until confirmed would make the
  feature useless during the hours that matter most. *Check: row C-02.*
- **`Rejected` events never affect projections** and are retained, not deleted —
  the audit trail is the point. *Check: row C-03.*
- **Every event has an `EffectiveFrom`**, and expired events stop applying at
  computation time rather than being cleaned up by a job. *Check: row C-04.*
- **A user override replaces the default deltas entirely**, and the fact that it
  was overridden is recorded. *Check: row C-05.*
- **Every event carries a source**: a URL, or `manual` with the user as source.
  *Check: row C-06.*
- **Magnitude is clamped to `[0,1]`** on write. *Check: row C-07.*

# MVP scope note

There is **no news scraper in the MVP**. Events are user-created through the
Context Review UI. Automated news ingestion and LLM-proposed events are
post-MVP — see [post_mvp_roadmap](../tasks/post_mvp_roadmap.md). The catalog is
built now so that when a proposer arrives it has a vocabulary to target rather
than inventing one.

# Change procedure

Adding an event type: this file (member + default row), the enum, the UI
picker, and a test asserting the defaults — one commit.

# Verification

`test_matrix_projection_draft.md`, rows C-01 through C-07.

# Implementation evidence

All fourteen event types, the three directions, the three verification states,
their default deltas, and their default expirations are defined once in
`ContextEventCatalog`. Creation clamps magnitude, requires a source, and always
starts at `Proposed`. Explicit human review records the reviewer and UTC review
time; the source tree contains exactly one assignment of `Verified`, inside
`ContextEvent.VerifyByHuman`. C-01 through C-07 cover the workflow, defaults,
source requirement, overrides, and magnitude clamp.
