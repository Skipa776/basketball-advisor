---
type: contract
title: Trade Contract
description: Before/after roster valuation with replacement backfill and slot displacement, category win-delta, legality, and the fairness boundary.
tags: [contract, trades, math]
source_paths: [src/FantasyBasketball.Domain/Trades, src/FantasyBasketball.Application/Trades]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trades]
depends_on: [category_value_contract.md, draft_value_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Value change accounts for slot displacement and replacement backfill, not just player totals.
  - An illegal trade is rejected as illegal, never scored as risky.
  - The tool never claims to judge whether a trade is fair to the other side.
---

# Responsibility

Owns trade evaluation. The naive version — sum the players on each side and
compare — is wrong for the same reason a raw projection board is wrong for
drafting: **what matters is the change to your startable lineup, not the change
to your player list.**

# Points leagues

```text
startingValue(roster) = Σ over the optimal legal starting assignment of
                            projectedSeasonValue(player)

ValueBefore = startingValue(currentRoster)
ValueAfter  = startingValue(currentRoster − sending + receiving)
```

Two effects the sum-the-players approach misses, both captured by recomputing the
starting assignment:

- **Slot displacement.** Receiving a third startable center adds far less than his
  raw value, because one of the three now sits.
- **Replacement backfill.** Sending two players for one leaves an open slot,
  filled at `ReplacementValue`
  ([draft_value_contract](draft_value_contract.md)) — a 2-for-1 is not a loss of
  one player's value.

```text
Delta = ValueAfter − ValueBefore   (+ ReplacementBackfill already inside ValueAfter)
```

Reported in bands rather than false precision:

| `|Delta| / ValueBefore` | Verdict |
|---|---|
| `< 0.02` | Neutral |
| `< 0.08` | Slight win / Slight loss |
| otherwise | Clear win / Clear loss |

# Category leagues

```text
ExpectedWinsDelta = Σ over non-punted categories of
                        ( P_after(win cat) − P_before(win cat) )
```

Units are **expected categories won per week** — the number the user actually
cares about, and directly interpretable against the 5-of-9 win condition. Both
profiles come from [category_value_contract](category_value_contract.md), and the
before/after category profile is displayed alongside the number.

A trade can raise raw value and lower expected wins (concentrating strength in
categories already won). When those disagree, **expected wins is the verdict in a
category league** and the divergence becomes a `RosterFit` evidence item.

# Legality

Checked before valuation, not scored as risk:

- Roster size after the trade must be legal.
- Every received player must be assignable to a legal slot.
- The trade must be before the league's trade deadline.

An illegal trade returns `validation_failed` naming the violated rule
([api_surface](api_surface.md)). Scoring an impossible trade is worse than
refusing it, because the score looks like advice.

# Multi-team trades

Evaluated **from the user's perspective only**. Other teams' sides are shown as
information — who sends and receives what — with no verdict attached.

# The fairness boundary

The tool does not judge whether a trade is fair to the other manager. It has no
model of their roster needs, their punt strategy, or their league politics, and a
"fair" verdict computed without those would be authoritative-looking noise.
Output is always *what this does to your team*. This is a deliberate scope
limit — recorded in [assumptions](../assumptions.md), not an oversight.

# Invariants

- **A 2-for-1 credits replacement backfill.** *Check:
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md)
  row R-01 asserts `ValueAfter` includes a replacement-level fill for the vacated
  slot.*
- **Slot displacement discounts a redundant addition.** *Check: row R-02 receives
  a third startable center and asserts the delta is far below his raw value.*
- **An illegal trade is `validation_failed`, never scored.** *Check: row R-03.*
- **Category verdict follows `ExpectedWinsDelta`, not raw value**, and a
  disagreement produces evidence. *Check: row R-04.*
- **A trade of a player for himself is exactly neutral.** *Check: row R-05 — the
  identity case that catches most valuation bugs.*
- **Multi-team trades produce exactly one verdict, for the user.** *Check: row R-06.*
- **Every evaluation carries evidence and a confidence level.** *Check: row R-07.*

# Change procedure

Changing the bands or the valuation: this file, the service, and rows
R-01 … R-07 — one commit.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md),
rows R-01 through R-07.
