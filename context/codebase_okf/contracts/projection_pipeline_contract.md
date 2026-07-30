---
type: contract
title: Projection Pipeline Contract
description: The four projection records, the per-minute baseline math, how context deltas apply, and a fully worked example to verify against.
tags: [contract, projections, math]
source_paths: [src/FantasyBasketball.Domain/Projections, src/FantasyBasketball.Application/Projections]
test_paths: [tests/FantasyBasketball.Domain.Tests/Projections]
depends_on: [stat_vocabulary.md, scoring_rules_catalog.md, context_event_catalog.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - The worked example below reproduces exactly, to 4 decimal places.
  - Observed, baseline, adjusted, and value are four separate persisted records.
  - No code path mutates an existing BaselineProjection row.
---

# Responsibility

Owns how a projection is computed and how context modifies it. The central rule
of the whole product lives here: **statistics are necessary but not sufficient**,
so the pipeline keeps the statistical baseline and the contextual adjustment as
separate, auditable numbers.

# The four records

Never merged, never collapsed into one table, never overwritten:

```text
ObservedStats        what actually happened
      ↓
BaselineProjection   statistics only — no news, no roles, no opinions
      ↓
AdjustedProjection   baseline + context events, referencing the baseline by id
      ↓
FantasyValue         adjusted projection scored under one league's rules
```

Shapes are in [`ARCHITECTURE.md`](../../../ARCHITECTURE.md).

# Baseline math

Constants are named, configurable via `DraftWeightOptions`-style options
binding, and listed at the end of this section. They are **judgment values, not
back-tested** — see [assumptions](../assumptions.md).

**Step 1 — per-minute rates.** For each counting stat except `MIN`:

```text
rate[k] = seasonTotals[k] / seasonTotals[MIN]
```

**Step 2 — shrink toward the league.** Small samples produce wild rates, so
shrink toward the pool average with a constant `K_RATE = 500` minutes:

```text
leagueAvgRate[k] = Σ totals[k] / Σ totals[MIN]     over players with GP ≥ 20
shrunk[k] = (playerMinutes × rate[k] + K_RATE × leagueAvgRate[k])
            / (playerMinutes + K_RATE)
```

The league average is computed from the imported pool, not a hardcoded table —
there is no magic constant to go stale.

**Step 3 — project minutes.** Established players are not shrunk:

```text
if GP ≥ GP_FULL (30):   projMPG = priorMPG
else:                   projMPG = (GP / GP_FULL) × priorMPG
                                + (1 − GP / GP_FULL) × MPG_PRIOR (20.0)
```

**Step 4 — multiply out.**

```text
projectedPerGame[k] = shrunk[k] × projMPG          for counting stats
projectedPerGame[MIN] = projMPG
```

**Step 5 — ratios.** Recomputed from the projected components per
[stat_vocabulary](stat_vocabulary.md), never projected directly.

**Step 6 — games played.** Regress durability toward a league prior:

```text
projGP = round( W_GP × priorGP + (1 − W_GP) × (DURABILITY × 82) )
         W_GP = 0.5,  DURABILITY = 0.85
```

| Constant | Value | Meaning |
|---|---|---|
| `K_RATE` | `500` | Shrinkage strength, in minutes |
| `GP_FULL` | `30` | Games above which minutes are not shrunk |
| `MPG_PRIOR` | `20.0` | Minutes shrinkage target |
| `W_GP` | `0.5` | Weight on the player's own durability |
| `DURABILITY` | `0.85` | League availability prior |

# Applying context

`AdjustedProjection` is computed *from* a `BaselineProjection`, referencing it by
id. Deltas come from `PlayerContextImpact`
([context_event_catalog](context_event_catalog.md)).

Application order — fixed, because it is not commutative:

1. **Minutes**: `adjMPG = clamp(projMPG + Σ MinutesDelta, 0, 42)`
2. **Rate multipliers**, applied to the shrunk per-minute rates:

   | Delta | Multiplies |
   |---|---|
   | `UsageDelta` | `PTS FGM FGA FG3M FG3A FTM FTA TOV` |
   | `ShotVolumeDelta` | same set as `UsageDelta` |
   | `AssistShareDelta` | `AST` |
   | `ReboundShareDelta` | `REB OREB DREB` |

3. **Multiply out** with `adjMPG`, then recompute ratios.
4. **Role risk**: `roleRisk = clamp(Σ RoleRiskDelta, 0, 1)`; confidence is
   reduced accordingly per
   [recommendation_evidence_contract](recommendation_evidence_contract.md).

# Invariants

- **The baseline is immutable.** Applying, changing, or removing context never
  writes to a `BaselineProjection` row. *Check:
  [test_matrix_projection_draft](../tests/test_matrix_projection_draft.md) row
  P-05 snapshots the baseline, applies and removes an event, and asserts the row
  is byte-identical.*
- **Usage and shot volume never compound.** If both are set for one player, the
  larger absolute delta wins; they are not multiplied together. Without this
  rule a single roster change gets counted twice. *Check: row P-06.*
- **All multipliers are clamped to `[0.5, 1.5]`** individually and in
  aggregate — no stack of events can halve or double a player twice over.
  *Check: row P-07 applies five large events and asserts the bound holds.*
- **Minutes are clamped to `[0, 42]`.** *Check: row P-07.*
- **Ratios are never projected directly.** *Check: row P-03.*
- **Expired events do not apply.** An event whose `ExpectedExpiration` has passed
  is excluded at computation time. *Check: row P-08.*
- **Persisted projection values are `decimal(10,4)`, rounded half-away-from-zero
  only at persistence.** Intermediates are never rounded. *Check: row P-02.*

# Worked example

Verbatim reproduction is a required test (row P-01). Seed league from
[scoring_rules_catalog](scoring_rules_catalog.md).

```text
Observed (prior season):   GP 50   MIN 1500 (30.0 MPG)
  totals: PTS 900  REB 300  AST 225  STL 60  BLK 30  TOV 150

League average per-minute: PTS 0.50  REB 0.18  AST 0.12
                           STL 0.03  BLK 0.02  TOV 0.07

Step 1  raw rates          PTS 0.6000  REB 0.2000  AST 0.1500
                           STL 0.0400  BLK 0.0200  TOV 0.1000

Step 2  shrink, K_RATE 500 weight = 1500/(1500+500) = 0.75
                           PTS 0.5750  REB 0.1950  AST 0.1425
                           STL 0.0375  BLK 0.0200  TOV 0.0925

Step 3  GP 50 ≥ 30         projMPG = 30.0

Step 4  per game           PTS 17.2500  REB 5.8500  AST 4.2750
                           STL  1.1250  BLK 0.6000  TOV 2.7750

Step 6  projGP             0.5×50 + 0.5×(0.85×82) = 25 + 34.85 = 59.85 → 60

FantasyValue (seed league)
  PTS 17.2500 × 1.0 = +17.2500
  REB  5.8500 × 1.2 =  +7.0200
  AST  4.2750 × 1.5 =  +6.4125
  STL  1.1250 × 3.0 =  +3.3750
  BLK  0.6000 × 3.0 =  +1.8000
  TOV  2.7750 × −1.0 = −2.7750
  ------------------------------
  per game     33.0825
  season total 33.0825 × 60 = 1984.9500
```

# Change procedure

Changing a constant or a step: this file, the options class, and every affected
golden — including recomputing the worked example above by hand — in one commit.

# Verification

`test_matrix_projection_draft.md`, rows P-01 through P-08.

# Implementation evidence

P-01 through P-10 cover the worked math, persistence-only four-decimal
half-away-from-zero rounding, derived ratios, deterministic projection,
zero-minute behavior, baseline byte immutability, non-compounding usage and shot
volume, stacked clamps, computation-time expiry, and baseline/event references.
`ObservedStats`, `BaselineProjection`, `AdjustedProjection`, and `FantasyValue`
persist as four separate records through forward migrations. Repository and
DbContext shape keep observed statistics and baselines append-only.
