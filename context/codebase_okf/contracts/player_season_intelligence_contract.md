---
type: contract
title: Seasonal Player Evidence and Draft Labels
description: Planned played-game distribution, availability evidence, and season-scoped labels for player and pre-draft views.
tags: [contract, draft, performance]
source_paths: [src/FantasyBasketball.Domain/Trends, src/FantasyBasketball.Web/src]
test_paths: []
depends_on: [player_heat_contract.md, box_score_storage_contract.md, scoring_rules_catalog.md, draft_value_contract.md, provenance_contract.md]
status: planned
last_updated: 2026-10-01
owners: [engineering]
risk_level: high
---

# Responsibility

This is the owner-requested future seasonal evidence feature. It does not promote
an unbuilt calculation to a live badge or change the existing draft-value
formula. The pre-draft room and player detail will use the same dated,
league-scored evidence; they will not independently calculate labels in React.

# Distribution and availability

Use the **previous regular season** relative to the selected draft season.
Score each completed appearance with the selected league's scoring rules. An
appearance with zero or negative fantasy points remains in the sample. Exclude
DNPs, incomplete games, other players, other seasons, and other game phases.
Reject duplicate player/game observations and retain source, game dates,
retrieval date, scoring version, and sample count.

The observed per-game mean and empirical 1st and 99th percentiles describe
**games played only**. They are descriptive tails, not prediction intervals for
next season. Show games played and independently sourced games available, plus
the observed availability ratio when the schedule is complete. Do not fill
missed games with zero in the percentile distribution. If schedule or game-log
coverage is incomplete, availability and any season estimate that depends on
it are unavailable, not zero. The season estimate must show its games assumption
and formula next to the result. Percentile interpolation, minimum sample size,
and availability estimation remain open decisions before executable code ships.

Existing `DraftValue` remains the pick recommendation's canonical total. Any
new number described as a player's **true value** must first specify its units,
league rules, replacement baseline, availability adjustment, and relationship
to that total in an approved revision of
[draft_value_contract](draft_value_contract.md), with offline calibration and
goldens. Do not add a second opaque score. Market ADP is a distinct observation;
FantasyNerds NBA draft ranking is a rank, not ADP. Show market source, timestamp,
and missing-market state rather than substituting rank for pick position.

# Label vocabulary and evidence

The canonical display labels for this feature are `CONSISTENT`, `HOT`, `COLD`,
`HIGH PEAKS`, and `INJURY PRONE`. Each label is scoped to a season, selected
league rules, and as-of date, with its sample count, criterion, observation
window, and provenance visible. Multiple labels may apply. Insufficient or stale
evidence means no label, with a reason shown in detail views. Historical labels
are recalculated from the selected season; a current label is never backfilled
into an earlier draft snapshot.

- `HOT` and `COLD` derive only from the disjoint latest-five versus preceding
  ten-to-thirty comparison and the thresholds owned by
  [player_heat_contract](player_heat_contract.md) (decided 2026-10-01); a negative
  shift alone must not be presented as an injury or forecast.
- `CONSISTENT` summarizes low dispersion in qualified played-game fantasy
  scores. Its statistic and threshold are not yet selected.
- `HIGH PEAKS` summarizes unusually high observed game scores relative to the
  player's own qualified distribution. Its statistic and threshold are not yet
  selected; the 99th percentile alone is not a future ceiling.
- `INJURY PRONE` requires verified dated injury history or an explicitly named,
  trustworthy missed-game reason source. Missing games, low availability, or a
  short sample alone cannot establish an injury cause. This label should be
  reviewed for wording and evidence before activation.

No label changes an immutable baseline projection or turns proposed context
into verified truth. Draft cards may show labels only after the associated
calculation and evidence API have offline tests and production data coverage.

# Verification to add with implementation

Pure offline goldens must cover league scoring changes, exact played-game
percentiles, DNP and incomplete exclusions, missing schedule coverage,
small samples, cross-season isolation, date cutoffs, cold versus insufficient
history, and injury evidence verification. HTTP/browser checks must prove that
labels and distribution evidence are league-scoped, source-attributed, and
absent on empty production data. These checks are planned, not passing today.
