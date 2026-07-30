---
type: contract
title: Category Value Contract
description: Z-score math with volume-weighted ratio categories, team profiles, matchup win probability, punt detection, and the punt-count ceiling.
tags: [contract, categories, math]
source_paths: [src/FantasyBasketball.Domain/Categories, src/FantasyBasketball.Application/Categories]
test_paths: [tests/FantasyBasketball.Domain.Tests/Categories]
depends_on: [scoring_rules_catalog.md, stat_vocabulary.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Ratio categories are volume-weighted; a low-volume high-percentage player is not an asset.
  - TOV inverts everywhere, in exactly one place in the code.
  - Punt count is capped by the win-condition arithmetic, not by taste.
---

# Responsibility

Owns category-league valuation: what a player is worth in a category league, what
a roster's category profile is, and which categories are worth abandoning. The
MVP scores categories ([scoring_rules_catalog](scoring_rules_catalog.md)); this
contract compares them.

# The player pool

Z-scores are meaningless without a defined population. The pool is the top
`TeamCount × rosterSize` players by projected minutes — i.e. roughly the set that
will actually be rostered. Using every NBA player instead would compress every
z-score toward the deep-bench mean and make real starters look identical.

# Counting categories

For `FG3M PTS REB AST STL BLK TOV`, on **projected weekly totals** (per-game
value × projected games that week):

```text
z(player, cat) = (weekly(player, cat) − mean(pool, cat)) / stdDev(pool, cat)
z(player, TOV) = −z as computed above
```

**`TOV` inversion lives here and nowhere else.** Every consumer reads the signed
z and never re-inverts.

# Ratio categories — volume weighted

For `FG_PCT` and `FT_PCT`, the naive z-score on the percentage is **wrong**: a
player who makes both his free throws has a 100% rate and near-zero effect on a
team's weekly percentage. Score the *impact* instead:

```text
impact(player, FG_PCT) = (FG_PCT(player) − FG_PCT(pool)) × FGA(player, weekly)
impact(player, FT_PCT) = (FT_PCT(player) − FT_PCT(pool)) × FTA(player, weekly)

z(player, ratioCat) = (impact − mean(pool, impact)) / stdDev(pool, impact)
```

where `FG_PCT(pool)` is the pool's **aggregate** rate (`Σ FGM / Σ FGA`), not the
mean of player rates — consistent with the no-averaging-ratios rule in
[stat_vocabulary](stat_vocabulary.md).

# Team profile

```text
ExpectedWeeklyTotal(cat) = Σ over projected starters of weekly(player, cat)
Percentile(cat)          = rank of this team's total among all league teams' totals
```

Ratio categories are recomputed from summed components, never summed directly.

# Matchup win probability

A weekly category total is modelled as `Normal(μ, σ)`:

```text
μ(cat)  = ExpectedWeeklyTotal(cat)
σ(cat)  = sqrt( Σ over starters of perGameVariance(player, cat) × gamesThisWeek )

P(win cat) = Φ( (μ_self − μ_opp) / sqrt(σ_self² + σ_opp²) )
```

`Φ` is the standard normal CDF, implemented from the Abramowitz–Stegun 7.1.26
`erf` approximation (absolute error < 1.5e-7) as
`Domain/Categories/NormalDistribution.cs`. **This is why
`MathNet.Numerics` is on the forbidden list** — the whole need is one function
with one test against published values.

`perGameVariance` for counting stats uses the pool's variance for players of
similar minutes; for ratio categories the variance of the *impact* term. A
player with no history uses the pool variance at his projected minutes.

# Punt detection

Punting is a strategy, not a defect. The ceiling is arithmetic, not preference:
winning a 9-category matchup requires **5 of 9**, so punting `k` leaves `9 − k`
categories from which 5 must be won — feasible only while `k ≤ 4`.

```text
PUNT_MAX = 3          # 4 is the arithmetic ceiling; 3 leaves one category of margin
PUNT_Z_FLOOR = −1.0
PUNT_WIN_CEILING = 0.35
```

A category is a **punt candidate** when the roster's summed z is `≤ PUNT_Z_FLOOR`
**and** its `P(win)` is `≤ PUNT_WIN_CEILING`. Candidates are ranked by how
expensive they would be to fix (the z-gap to the median team) and the top
`PUNT_MAX` are proposed. A user-chosen punt set always overrides detection and is
recorded as `UserChosen = true`.

# Marginal value to a roster

```text
CategoryValue(player) = Σ over non-punted categories of w(cat) × z(player, cat)

w(cat) = 1.0   when P(win cat) < 0.45          # a category in play and losing
       = 0.5   when 0.45 ≤ P(win cat) ≤ 0.55   # a coin flip, worth reinforcing
       = 0.25  when P(win cat) > 0.55          # already winning; marginal
       = 0.0   for punted categories
```

This is the design doc's *absolute value versus marginal value to this roster*
distinction, made concrete: a scoring specialist added to a team already winning
`PTS` scores `0.25 × z`, while a steals specialist on a team losing `STL` scores
the full `z`.

# Invariants

- **Ratio categories are volume weighted.** *Check:
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md)
  row Y-01 gives a 100%-on-two-attempts player and asserts his `FT_PCT` z is
  near zero, not maximal.*
- **`TOV` is inverted in exactly one place.** *Check: row Y-02, plus a grep
  asserting a single inversion site.*
- **Pool aggregate rates are used, never means of rates.** *Check: row Y-03.*
- **`Φ` matches published values** to 1e-6 at `z ∈ {−3, −1, 0, 1, 1.96, 3}`.
  *Check: row Y-04.*
- **`P(win)` is symmetric**: swapping self and opponent gives `1 − p`. *Check: row Y-05.*
- **Punt count never exceeds `PUNT_MAX`**, and a user punt set overrides
  detection. *Check: row Y-06.*
- **Punted categories contribute exactly zero** to `CategoryValue`. *Check: row Y-07.*
- **Zero-variance categories do not divide by zero** — identical totals yield
  `P(win) = 0.5`. *Check: row Y-08.*

# Change procedure

Changing a weight band or threshold: this file, the analyzer, and rows
Y-01 … Y-08 — one commit. Adding a category: the catalog first.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md),
rows Y-01 through Y-08.
