---
type: component
title: Streaming Advisor
description: Usable-game calculation, multi-day planning under an acquisition limit, and the plan-explanation surface.
tags: [component, streaming, optimization]
source_paths: [src/FantasyBasketball.Domain/Streaming, src/FantasyBasketball.Application/Streaming]
test_paths: [tests/FantasyBasketball.Domain.Tests/Streaming]
depends_on: [../contracts/streaming_contract.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: high
done_criteria:
  - Every move in a plan states the games it buys and what it costs.
  - No plan is emitted that the league's rules would reject.
---

# Responsibility

Owns the streaming plan. The algorithm, factors, and drop protection are owned by
[streaming_contract](../contracts/streaming_contract.md).

# Design

- `UsableGameCalculator` is pure: `(roster, schedule, lineupRules, horizon) →
  UsableGame[]`. Every entry carries a `Reason`, including the negative ones — "no
  open UTIL slot; contested by <player>" is the answer to the question the user is
  actually asking.
- `StreamingPlanner` runs the greedy pass plus one improvement pass and returns a
  `StreamingPlan` — an ordered list of dated moves, not a single "add this player".
  The design doc's Monday/Wednesday/Saturday sequence is the output shape.
- Legality is checked inside the planner, not validated afterwards: an illegal
  candidate is never scored, so an illegal plan cannot be the argmax.
- Both lineup cadences are separate code paths sharing one interface. Weekly is not
  daily with a bigger horizon.

# The plan is an explanation

A bare sequence of adds and drops is unusable — the user has to decide whether to
trust it. Each `StreamingMove` renders with:

```text
Wed  Add <player>   +3 usable games (Thu, Sat, Sun) — Fri has no open slot
     Drop <player>  0 usable games remaining this week; rest-of-season value below threshold
     net +8.4 expected fantasy points, 2 of 4 acquisitions used
```

Cumulative acquisition use is always visible, because the limit is the constraint
the user is really managing.

# Invariants

- **`UsableGameCalculator` is pure.** *Check: its signature.*
- **Every `UsableGame` carries a reason, usable or not.** *Check:
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md) row S-29.*
- **Illegal candidates are never scored.** *Check: row S-24.*
- **Weekly and daily cadences take different paths** and both are tested. *Check:
  rows S-20 and S-35.*
- **Plans are deterministic**, ties broken by player id. *Check: row S-28.*
- **Acquisition usage is reported and never exceeded.** *Check: row S-23.*
- **A plan with no positive move returns an empty plan**, not a forced move.
  *Check: row S-36.*

# Change procedure

Changing the algorithm keeps the interface and must still satisfy rows S-20 … S-36.
Changing a factor means the contract first.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md), rows
S-20 through S-36.

## Status — 2026-09-23

The domain engine is built and tested (see streaming_contract). The application
service, API and UI that feed it league rosters, schedule and per-game values are
the next step.

## Wired — 2026-09-23

`StreamingService` builds the week from "today" (`Landing:AsOf` when set) through
Sunday: the user's marked team, the league's roster slots, cadence and weekly
acquisition limit, and the top 40 free agents (not on any imported roster) by
last-10 average under league scoring. Game days come from the stored schedule for
each player's current team; rest-of-season value is the per-game average over the
team's remaining scheduled games. `GET /api/leagues/{id}/streaming` returns the
dated plan with each move's usable games bought and lost and acquisitions used;
`/app/streaming` renders it (or an explained hold). On the replayed week of
2026-03-04 it proposed adding Jusuf Nurkić and Reed Sheppard for Kyrie Irving and
Tyrese Haliburton (no 2025-26 games), +235.5 usable points, in 0.8 s.

Not built: acquisitions already used this week (the plan assumes the full limit),
user-marked protection, and availability/back-to-back factors (no injury feed).
