---
type: contract
title: Recommendation and Evidence Contract
description: Evidence shape and kinds, the confidence enum and how it is derived, and the rule that no score ships without evidence.
tags: [contract, explainability, recommendations]
source_paths: [src/FantasyBasketball.Domain/Recommendations]
test_paths: [tests/FantasyBasketball.Domain.Tests/Recommendations]
depends_on: [provenance_contract.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
edit_policy: stable_contract
done_criteria:
  - No recommendation with an empty evidence list can be constructed.
  - Confidence is derived from named factors, never assigned ad hoc.
  - The engine returns structured evidence; the UI does the wording.
---

# Responsibility

Owns explainability. The product rule is absolute: **the application never shows
a number it cannot justify.** A bare `Stream Score: 87.4` is a defect, not a
display choice.

# Evidence

```text
EvidenceKind     = Minutes | Usage | Opportunity | Efficiency | Schedule
                 | RosterFit | Scarcity | Market | Context | Injury
                 | SampleSize | DataQuality
EvidencePolarity = Supporting | Risk | Neutral
```

`RecommendationEvidence(Kind, Polarity, Statement, Magnitude?)` — shape in
[`ARCHITECTURE.md`](../../../ARCHITECTURE.md).

`Statement` is a short factual clause with its numbers already in it
(`"minutes up 22.4 → 31.7"`). The engine produces structured evidence; the UI
groups and formats it. **The UI never computes an explanation and the engine
never emits pre-formatted markup.**

# Confidence

```text
Confidence = High | Moderate | Low | Speculative
```

Four coarse levels on purpose. A percentage would imply precision this model
does not have.

Derived from a `0..1` score, then bucketed. Factors, each in `0..1`:

| Factor | Weight | Meaning |
|---|---|---|
| Sample size | `0.30` | Minutes and games behind the projection, relative to `K_RATE` |
| Role stability | `0.25` | `1 − roleRisk` from the adjusted projection |
| Data freshness | `0.20` | Decays with staleness of the newest contributing import |
| Source quality | `0.15` | Mean provenance confidence of contributing rows |
| Context certainty | `0.10` | Mean confidence of applied context events; `1.0` when none apply |

```text
score = Σ (factor × weight)

score ≥ 0.75  → High
score ≥ 0.55  → Moderate
score ≥ 0.35  → Low
otherwise     → Speculative
```

# Invariants

- **A `Recommendation` with an empty evidence list cannot exist.** Enforced in
  the constructor, not by convention — construction throws. *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row
  E-01 asserts the throw, and row D-06 asserts every draft recommendation
  carries evidence.*
- **Every risk shown is a real `Risk`-polarity evidence item**, not prose in the
  UI. *Check: row E-02.*
- **Confidence comes from the factor model**, never assigned inline. *Check: row
  E-03 varies each factor independently and asserts the bucket moves.*
- **Degraded data lowers confidence visibly.** A stale or failed import
  reduces the freshness factor and surfaces a `DataQuality` evidence item —
  the app degrades loudly, never silently. *Check: row E-04.*
- **Evidence is ordered**: supporting items by descending magnitude, then risks.
  Deterministic ordering keeps golden tests stable. *Check: row E-05.*
- **No fabricated precision.** Magnitudes are rounded to one decimal for
  display; the engine never emits more precision than its inputs justify.

# Change procedure

Adding an `EvidenceKind` touches this file, the enum, the producing engine, and
the UI grouping — one commit. Changing a confidence weight touches this file and
row E-03's expectations.

# Verification

`test_matrix_projection_draft.md`, rows E-01 through E-05.

# Implementation evidence

The canonical evidence kinds, polarities, confidence buckets, factor formula,
deterministic ordering, and valid-by-construction `Recommendation` are
implemented. E-01, E-02, E-03, and E-05 have Domain coverage, and every draft
recommendation carries ordered evidence. `DraftBoardService` derives freshness
from persisted import health; stale or failed automated sources lower the
factor and add a risk-polarity `DataQuality` item, completing E-04. The
recommendation and its ordered evidence also round-trip as separate persisted
records.
