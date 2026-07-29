---
type: component
title: Scoring Engine
description: Turns a stat line plus a league's rules into fantasy value, for both points and category leagues.
tags: [component, scoring, domain]
source_paths: [src/FantasyBasketball.Domain/Scoring]
test_paths: [tests/FantasyBasketball.Domain.Tests/Scoring]
depends_on: [../contracts/scoring_rules_catalog.md, ../contracts/stat_vocabulary.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - Every seed-league golden case passes.
  - Points and category leagues are both supported by the same entry point.
---

# Responsibility

The first milestone of the whole project and the piece everything else depends
on: given a stat line and a league, what is this worth *in that league*?
Semantics and the seed league are owned by
[scoring_rules_catalog](../contracts/scoring_rules_catalog.md).

# Design

One interface, two implementations selected by `LeagueType`:

```csharp
public interface IScoringEngine
{
    LeagueType Handles { get; }
    FantasyValue Score(StatLine line, FantasyLeague league);
    IReadOnlyDictionary<StatKey, decimal> ScoreCategories(StatLine line, FantasyLeague league);
}
```

`PointsScoringEngine` sums configured rules. `CategoryScoringEngine` returns
per-category totals. Two implementations of one interface is the *only*
justification for the interface — do not add a factory, a registry, or a
strategy pattern on top of a switch over two cases.

# Invariants

- **Pure and deterministic.** No clock, no I/O, no caching. The same inputs
  always produce the same output. *Check: `test_matrix_scoring.md` row S-04.*
- **Unscored stats contribute zero**, never a default. *Check: row S-05.*
- **Negative rule values work correctly**, including when they make total value
  negative. A bench player with more turnovers than production has negative
  value and the engine must say so. *Check: row S-06.*
- **Ratio stats are recomputed on aggregation, never averaged.** *Check: rows
  S-07 and S-08.*
- **Scoring a full player pool is fast enough for live re-ranking** — it runs
  inside the draft board's per-pick budget. No per-call allocation of the rule
  set. *Check: the R7 latency assertion in
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md).*
- **Category scoring returns totals only.** Comparison, z-scores, and punt
  detection are post-MVP and must not leak in here. *Check: review; the
  interface has no comparison method to tempt anyone.*

# Change procedure

Changing scoring semantics: the catalog first, then this engine, then every
golden — one commit.

# Verification

[test_matrix_scoring](../tests/test_matrix_scoring.md), rows S-01 through S-08.
