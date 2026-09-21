---
type: contract
title: Player Heat and Recent Performance
description: Descriptive league-point heat against a disjoint expanding-then-rolling appearance baseline.
tags: [contract, heat, scoring]
source_paths: [src/FantasyBasketball.Domain/Trends]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trends/PlayerHeatTests.cs]
depends_on: [scoring_rules_catalog.md, provenance_contract.md, rolling_window_contract.md]
status: implemented
last_updated: 2026-09-20
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

# Verification

`PlayerHeatTests` checks every boundary above, shuffled input, DNP/zero/negative
values, zero baseline, season isolation, future/incomplete exclusions, duplicate
rejection, configured league scoring, independent current/comparison averages,
and rankings that respond to new games and changed rules. Import/storage/API/UI
remain separate milestones; successful math tests do not promote those modules.
