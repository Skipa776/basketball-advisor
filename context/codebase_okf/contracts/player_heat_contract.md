---
type: contract
title: Player Heat and Recent Performance
description: Descriptive league-point heat against a disjoint expanding-then-rolling appearance baseline.
tags: [contract, heat, scoring]
source_paths: [src/FantasyBasketball.Domain/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends/PlayerHeatTests.cs]
depends_on: [scoring_rules_catalog.md, provenance_contract.md, rolling_window_contract.md]
status: implemented
last_updated: 2026-10-01
owners: [engineering]
risk_level: high
---

# Responsibility

Owns the owner's selected descriptive heat calculation. This is distinct from
opportunity-weighted `TrendScore` and does not alter its contract, projections,
or draft-value weights. A shooting streak can be above baseline regardless of
its sustainability. No forecast or confidence estimate is implied.

# Canonical policy

`PlayerHeatOptions` is the sole executable owner of the selected window sizes:
recent three completed appearances, minimum ten baseline appearances, maximum
thirty baseline appearances. Values are passed to the pure calculator as options;
any future application binding must validate them. Results carry the effective
policy and model version. React must never reconstruct these windows.

Filter to the requested player and season, completed games on or before the
through-date, and appearances (`DidPlay`). A DNP is not a zero. A completed
appearance scoring zero or a negative value remains in the sample. Incomplete
games and future dates do not contribute. Duplicate game IDs for the same player
in the selected history are rejected, not silently counted or arbitrarily chosen.
Sort chronologically, then by game ID for deterministic same-date ordering.
Every sample carries provenance; missing final statistics are invalid.

Let `n` be the eligible appearance count:

- Recent window: last three appearances. Average is absent until all three exist.
- Comparison baseline: the appearances **before** that window, capped at the
  latest thirty; its average is absent until it has at least ten appearances.
- Current average: latest up-to-thirty appearances **including** the recent
  window, available after ten appearances. This is separate from the comparison.
- Points above baseline: recent average minus comparison average, only when both
  are available. Positive means above baseline; zero means unchanged; negative
  means below baseline. Insufficient data yields null, never a cold label.
- Relative lift: points above baseline divided by the **absolute** baseline
  average. Zero baseline yields null, so improvement from a negative average has
  the correct sign and zero never produces an infinite percentage.

First full comparison: appearance 13 uses 11–13 versus 1–10. At 33 use 31–33
versus 1–30; at 34 use 32–34 versus 2–31; at 40 use 38–40 versus 8–37.
No rounding occurs in computation. Display formatting belongs to the UI.

# Rankings and evidence

Best-performing means highest qualified **current** points-per-appearance average.
Hottest means the largest positive **absolute fantasy-point** lift against the
comparison baseline. Relative lift is supplementary evidence, not a second hidden
weight. Ties use player ID. Ranking recomputes every player's values under the same
league rules, season, date and policy; it never combines cached scoring versions.
Neither list includes an unqualified player. No cross-user owned data is needed.

Results include scored game IDs, dates and provenance for current, baseline and
recent samples, sample counts, the latest appearance date, effective policy and
model version. These are retrospective statistics as of a game-date cutoff,
**not** a claim about what information was available at a historical timestamp.
Data freshness must be checked by the future application/API integration before
labelling anything as a *live* streak. No stale-data or sustainability guarantee
is invented by this pure calculator.

# HOT and COLD labels (owner-approved 2026-10-01)

Labels are a separate, descriptive layer over the same disjoint windows. The
three-appearance averages above stay as they are; a label compares the latest
**five** appearances against the preceding 10–30, because three games give a
standard error too wide for a 0.75 bar for most players.

- The shift is an empirical-Bayes posterior: the player's variance is shrunk toward
  the pool's (CV by minutes, weight `nu`), and the shift toward zero (spread `tau`
  relative to the baseline mean). Both are fitted offline as model `heat-prior`
  ([model_params_contract](model_params_contract.md)).
- `HOT` when P(shift > floor) ≥ 0.75, `COLD` when P(shift < −floor) ≥ 0.75, floor
  2 fantasy points. A label stays until its probability falls below 0.60.
- Only players averaging 15+ baseline minutes and a positive baseline are labelled.
- Each label carries a cause from the minutes × usage × efficiency decomposition
  ([rolling_window_contract](rolling_window_contract.md)): `Role` at signed
  opportunity share ≥ 0.60, `Shooting` at ≤ 0.30, otherwise `Mixed`.
- A label describes the past. It is never a forecast, an injury claim, or an input
  to any recommendation.

# Verification

`PlayerHeatTests` checks every boundary above, shuffled input, DNP/zero/negative
values, zero baseline, season isolation, future/incomplete exclusions, duplicate
rejection, configured league scoring, independent current/comparison averages,
and rankings that respond to new games and changed rules. Import/storage/API/UI
remain separate milestones; successful math tests do not promote those modules.

## Dated query/UI integration — 2026-09-21

The authenticated points-league query and React recorded-performance views now
consume this calculator under
[player_performance_api_contract](player_performance_api_contract.md). Effective
policy remains owned here and is returned to React. All views are explicitly dated
and completeness/freshness unverified; no live streak label is enabled. No math,
`TrendScore`, projection or draft-value weight changed. A production game importer
is still absent.

## Configurable windows — 2026-09-23

`PlayerHeatOptions` binds from the `Heat` configuration section
(`Heat__RecentGames`, `Heat__MinimumBaselineGames`, `Heat__MaximumBaselineGames`).
Unset values keep the accepted policy above (3 / 10 / 30); invalid values fail
at startup. Lowering the minimum is an explicit owner test override for short
imported windows — responses always carry the effective `policy`, so a relaxed
run is visible, never silent.
