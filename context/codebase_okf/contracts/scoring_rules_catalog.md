---
type: contract
title: Scoring Rules Catalog
description: League types, roster slot vocabulary, scoring semantics, the seed league values, and the canonical category set.
tags: [contract, catalog, scoring]
source_paths: [src/FantasyBasketball.Domain/Leagues]
test_paths: [tests/FantasyBasketball.Domain.Tests/Scoring]
depends_on: [stat_vocabulary.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - A points league and a category league can both be configured and scored.
  - No provider scoring value is silently substituted for user configuration.
  - The seed league reproduces its golden values exactly.
---

# Responsibility

Owns league type and roster vocabulary, the meaning of a scoring rule, and the
seed league that every golden test is written against. Stat keys come from
[stat_vocabulary](stat_vocabulary.md); this file never redefines them.

# Enums

```text
LeagueType      = Points | Categories
LineupCadence   = Daily | Weekly
RosterSlotKind  = PG | SG | SF | PF | C | G | F | UTIL | BENCH | IR
```

`G` accepts `PG`/`SG`; `F` accepts `SF`/`PF`; `UTIL` accepts any. Eligibility is
a property of the slot, not of the player.

# Scoring semantics

**Points league** — fantasy value is the sum over configured rules:

```text
value = Σ  statLine[rule.Stat] × rule.PointsPerUnit
```

- A stat with no rule contributes **zero**, never a default.
- `PointsPerUnit` may be negative (turnovers) or fractional.
- Ratio keys are legal in a rule but unusual; if configured, the ratio is used
  as its `0..1` decimal.

**Category league** — value is not a single number. The engine returns the
per-category totals for the league's configured categories. Ratio categories are
recomputed from component counting stats per
[stat_vocabulary](stat_vocabulary.md), never averaged.

Comparing category teams (z-scores, percentiles, punt detection) is **post-MVP** —
see [post_mvp_roadmap](../tasks/post_mvp_roadmap.md). The MVP scores categories;
it does not rank them.

# The seed league

Canonical fixture for every golden test. Taken from the design doc's
points-league example.

```text
Name        : Seed Points League
Type        : Points
TeamCount   : 10
Cadence     : Daily

PTS  = +1.0
REB  = +1.2
AST  = +1.5
STL  = +3.0
BLK  = +3.0
TOV  = -1.0

Roster: PG SG SF PF C G F UTIL UTIL BENCH×3 IR×1
```

Stats not listed above (`MIN`, `FGM`, `FG3M`, `PF`, …) have **no rule** and so
contribute zero. That is a deliberate test case, not an omission.

## Canonical category set

For category leagues, the standard nine:

```text
FG_PCT  FT_PCT  FG3M  PTS  REB  AST  STL  BLK  TOV
```

`TOV` is the one category where lower is better. That inversion lives here and
nowhere else.

# Invariants

- **No provider defaults, ever.** ESPN/Yahoo/Sleeper default scoring never
  appears as a fallback value. A league with no scoring rules is invalid, not
  defaulted. *Check: `test_matrix_scoring.md` row S-01.*
- **A points league requires ≥1 scoring rule; a category league requires ≥1
  category.** Validation names the missing field. *Check: row S-02.*
- **A stat may appear in at most one scoring rule per league.** Duplicates are a
  validation error, not a silent last-wins. *Check: row S-03.*
- **`TOV` is inverted in category comparison** wherever categories are compared.
  *Check: post-MVP, when the analyzer exists. Prose-only until then — recorded
  in [assumptions](../assumptions.md).*
- **Seed values are canonical here only.** No code — test, fixture, or seed
  migration — retypes `1.2` or `3.0`; code reads the seed league constant.
  *Check: the canonical-value grep over `src/` and `tests/`.*
- **Worked examples are the one sanctioned restatement.** Documentation that
  shows the arithmetic necessarily prints the values. There are exactly two such
  sites — the golden in
  [test_matrix_scoring](../tests/test_matrix_scoring.md) and the worked example
  in
  [projection_pipeline_contract](projection_pipeline_contract.md) — and both are
  recomputed by hand in the same commit as any seed change. Adding a third site
  is a review finding, not a convenience.

# Change procedure

Changing seed values updates this file and regenerates every scoring golden in
the same commit. Adding a `LeagueType` touches the engine, validation, the API
DTO, and the UI league form together.

# Verification

`test_matrix_scoring.md` — the seed league golden cases, the validation cases,
and the zero-contribution case for unscored stats.

## Explicit starter profile — owner-approved 2026-09-19

The React setup catalog offers an **explicitly selected**, editable ESPN default
points profile. `LeagueSetupCatalog` owns its executable weights; clients consume
that catalog and submit reviewed rules through the existing validated endpoint.
This narrows the historical ban on provider values, but preserves its invariant:
missing scoring rules are invalid and unconfigured stats contribute zero.
The seed league and existing golden values are unchanged. Profile reference:
[ESPN default points scoring](https://www.espn.com/fantasy/basketball/story/_/id/30296896/espn-fantasy-default-points-league-scoring-explained).
