---
type: contract
title: Stat Vocabulary
description: The canonical StatKey catalog, units, derivation rules, and every external source's column mapping. The keystone contract.
tags: [contract, catalog, stats]
source_paths: [src/FantasyBasketball.Domain/Stats]
test_paths: [tests/FantasyBasketball.Domain.Tests/Stats]
depends_on: []
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Every StatKey member in code matches this catalog exactly, no additions.
  - Every scraper column map and JSON field map resolves only to keys defined here.
  - Ratio keys are never summed anywhere in the codebase.
---

# Responsibility

Owns the complete stat vocabulary. **This is the keystone contract** — EF
columns, parser column maps, scoring rules, test fixtures, golden files, and API
DTOs all key off this file. A stat that is not here does not exist. Adding one
touches every artifact listed above, in one commit.

# The catalog

## Counting stats

Non-negative `decimal`. Stored both per-game and as season totals.

| Key | Meaning |
|---|---|
| `MIN` | Minutes played |
| `PTS` | Points |
| `OREB` | Offensive rebounds |
| `DREB` | Defensive rebounds |
| `REB` | Total rebounds |
| `AST` | Assists |
| `STL` | Steals |
| `BLK` | Blocks |
| `TOV` | Turnovers |
| `FGM` | Field goals made |
| `FGA` | Field goals attempted |
| `FG3M` | Three-pointers made |
| `FG3A` | Three-pointers attempted |
| `FTM` | Free throws made |
| `FTA` | Free throws attempted |
| `PF` | Personal fouls |

## Ratio stats

| Key | Derived from |
|---|---|
| `FG_PCT` | `FGM / FGA` |
| `FT_PCT` | `FTM / FTA` |
| `FG3_PCT` | `FG3M / FG3A` |

Not stats, deliberately absent from `StatKey`:

- **Usage rate** is carried as `SeasonStatLine.UsageRate`, not as a `StatKey`,
  so nothing can accidentally assign it a scoring value or sum it.
- **Games played** is `SeasonStatLine.GamesPlayed`, an `int`, for the same reason.

# Invariants

- **Ratio keys are computed, never summed and never stored as a source of
  truth.** Aggregating a percentage across players or games by addition or
  simple mean is wrong; always recompute from the component counting stats.
  *Check: `test_matrix_scoring.md` row S-07 aggregates two stat lines and
  asserts `FG_PCT` equals combined `FGM / FGA`, not the mean of the two.*
- **A zero denominator yields `0m`, never a divide-by-zero and never `null`.**
  A player with 0 FGA has `FG_PCT` of 0. *Check: row S-08.*
- **`REB == OREB + DREB`** for any imported line. A source that violates this is
  a parse error, not a rounding tolerance. *Check: parser tests assert the
  identity on every fixture row.*
- **All ratios are stored as `0..1` decimals**, never as `0..100` percentage
  points. Sources that publish percentage points are divided at the parser
  boundary, never downstream. *Check: row I-05 asserts a fixture's `24.5` usage
  becomes `0.245`.*
- **Money-like and aggregate stat values are `decimal`, never `float`/`double`.**
  *Check: the `float |double ` forbidden-pattern scan in
  [`stack_config.toml`](../../../stack_config.toml).*
- **A missing stat reads as `0m`, not null.** `StatLine`'s indexer guarantees
  this so scoring never needs a null check.

# Source column maps

The only place external names are translated. Parsers reference this table;
they do not carry their own literals.

## Basketball-Reference season tables

Page column header → `StatKey`. Applies to
`/leagues/NBA_{year}_per_game.html` and `_totals.html`.

| Header | Key | | Header | Key |
|---|---|---|---|---|
| `MP` | `MIN` | | `FT` | `FTM` |
| `PTS` | `PTS` | | `FTA` | `FTA` |
| `ORB` | `OREB` | | `PF` | `PF` |
| `DRB` | `DREB` | | `FG%` | `FG_PCT` |
| `TRB` | `REB` | | `3P%` | `FG3_PCT` |
| `AST` | `AST` | | `FT%` | `FT_PCT` |
| `STL` | `STL` | | `G` | *(→ `GamesPlayed`)* |
| `BLK` | `BLK` | | | |
| `TOV` | `TOV` | | | |
| `FG` | `FGM` | | | |
| `FGA` | `FGA` | | | |
| `3P` | `FG3M` | | | |
| `3PA` | `FG3A` | | | |

From `/leagues/NBA_{year}_advanced.html`: `USG%` → `SeasonStatLine.UsageRate`,
divided by 100.

## balldontlie

balldontlie's free tier supplies teams, players, and games only — **no stat
lines** — so it contributes no stat mappings. See
[provider_contracts](provider_contracts.md).

# Change procedure

Adding or renaming a key touches, in one commit: this file, `StatKey`, the EF
configuration and a migration, every source column map above, every HTML
fixture expectation, every scoring golden, and any API DTO that names stats.

# Verification

- Domain tests assert the `StatKey` enum members equal this catalog exactly.
- A canonical-value grep (see [run_quality_gates](../tasks/run_quality_gates.md))
  asserts no stat-name string literal appears outside this file and the enum.
- Parser tests assert `REB == OREB + DREB` and the `0..1` ratio range on every
  fixture row.

# Implementation evidence

`StatSourceColumnMap.SeasonTableCounting` is the single code map used by the
Basketball-Reference parser for every source counting column. A domain test
pins it to this table; fixture tests pin `USG%` boundary conversion and reject
any row where `REB != OREB + DREB`. Ratio stats continue to be derived by
`StatLine` from their counting components rather than accepted as source truth.
