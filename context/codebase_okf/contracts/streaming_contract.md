---
type: contract
title: Streaming Contract
description: The usable-game algorithm, streaming value, multi-transaction planning under an acquisition limit, and the drop-protection rule.
tags: [contract, streaming, optimization]
source_paths: [src/FantasyBasketball.Domain/Streaming, src/FantasyBasketball.Application/Streaming]
test_paths: [tests/FantasyBasketball.Domain.Tests/Streaming]
depends_on: [projection_pipeline_contract.md, category_value_contract.md]
status: partial
last_updated: 2026-09-23
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - A four-game week can score below a three-game week when slots are the constraint.
  - No plan exceeds the league's acquisition limit or leaves the roster illegal.
  - No plan drops a player worth more than the gain it buys.
---

# Responsibility

Owns streaming: which free agent to add, when, and what to drop. The product rule
is the whole feature — **a game only counts if the player can actually be
started that day.** Four scheduled games on nights when your lineup is already
full is worth less than three games on quiet nights.

# Fixing a formula error in the design doc

The design doc's streaming score multiplies `ExpectedPerGameValue × UsableGames ×
… `. Summing per-day value and *then* multiplying by the count of usable days
counts every day twice over. The correct form sums across days:

```text
StreamingValue(player, horizon) =
    Σ over usable days d of
        adjustedPerGameValue(player)
      × AvailabilityProbability(player, d)
      × MatchupAdjustment(player, d)
      × RoleConfidence(player)
      × TeamNeedAdjustment(player, league)
```

`UsableGames` remains a reported number — it is what the user reasons about —
but it is the *cardinality of the sum*, never a multiplier on it.

# Usable games

## Daily-lineup leagues

For each day `d` in the horizon:

1. Collect roster players with an NBA game on `d` who are not `Out`.
2. Order candidates by **eligibility restriction ascending, then expected value
   descending** — most-constrained first.
3. Assign greedily to open eligible lineup slots.
4. A player's game is `Usable` when he received a slot; otherwise it is not, and
   the `Reason` records which slot was contested and by whom.

Most-constrained-first is the fix for the obvious greedy failure: a center-only
player must claim `C` before a `UTIL`-flexible star takes it.

> `ponytail:` greedy with restriction ordering, not exact maximum-weight bipartite
> matching. With ~10 slots and ~13 players the exact solution is cheap if a real
> case shows greedy losing value — the upgrade is an augmenting-path matcher
> behind the same interface.

## Weekly-lineup leagues

The lineup is set once, so the branch is different: a started player's usable
games are **all** his scheduled games that week, and a benched player's are zero.
The planner then chooses which players to start for the week rather than which to
slot each day. Both cadences are required
([scoring_rules_catalog](scoring_rules_catalog.md) owns `LineupCadence`).

# Per-day factors

| Factor | Definition |
|---|---|
| `AvailabilityProbability` | `0` when `Out`; `B2B_PENALTY` (0.95) on the second night of a back-to-back for players with an active `RestRisk` context event; otherwise `1.0` |
| `MatchupAdjustment` | `clamp(1 + MATCHUP_SENSITIVITY × (oppAllowedFantasyPoints / leagueMean − 1), 0.90, 1.10)`, `MATCHUP_SENSITIVITY = 0.5`. **`1.0` with a `Schedule` evidence item when opponent data is absent** — never a guessed adjustment |
| `RoleConfidence` | `1 − roleRisk` from the adjusted projection |
| `TeamNeedAdjustment` | `1.0` in points leagues; the `w(cat)`-weighted category value ratio in category leagues ([category_value_contract](category_value_contract.md)) |

# Multi-transaction planning

```text
maximize   Σ ExpectedIncrementalValue(move)
subject to acquisitions ≤ league weekly limit
           roster remains legal after every move
           no protected player is dropped
```

Algorithm: greedy over decision days — at each day take the highest-gain legal
`(add, drop)` pair while gain `> 0` and budget remains — followed by **one
improvement pass** that tries replacing each chosen move with the best
alternative for that day and keeps the swap if total value rises.

> `ponytail:` greedy plus one improvement pass. The exact form is a DP over
> `(day, acquisitionsUsed)` and is the upgrade if measurement shows the greedy
> plan trailing; the interface does not change.

`ExpectedIncrementalValue(add, drop) = StreamingValue(add, remaining horizon) −
StreamingValue(drop, remaining horizon)`.

# Drop protection

A streamer is a short-horizon bet; a rostered contributor is not.

```text
A player may not be dropped when:
  restOfSeasonValue(player) > KEEP_MULTIPLE × ExpectedIncrementalValue(the move)
  or the user has marked him protected
  or he occupies an IR slot
                                          KEEP_MULTIPLE = 1.5
```

This is the rule that stops the optimizer trading a rest-of-season starter for a
three-game weekend.

# Invariants

- **A four-game week can lose to a three-game week.** *Check:
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md)
  row S-20 constructs exactly that case and asserts the ordering.*
- **The daily slot assignment never over-fills a slot** and never assigns an
  ineligible player. *Check: row S-21.*
- **Most-constrained-first ordering is applied** — a `C`-only player is not
  displaced by a `UTIL`-eligible higher scorer when both can be started.
  *Check: row S-22.*
- **No plan exceeds the acquisition limit.** *Check: row S-23.*
- **Every move leaves the roster legal.** *Check: row S-24.*
- **Drop protection holds.** *Check: row S-25 offers a small gain for a valuable
  player and asserts no drop.*
- **A day with no open slots yields zero usable games**, not a smaller positive
  number. *Check: row S-26.*
- **Missing opponent data yields `MatchupAdjustment == 1.0` plus evidence**, never
  an imputed strength. *Check: row S-27.*
- **The plan is deterministic** for identical inputs — ties broken by player id.
  *Check: row S-28.*

# Change procedure

Changing a factor or a constant: this file, the calculator, and rows
S-20 … S-28 — one commit. Changing the assignment algorithm requires row S-22 to
still pass.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md),
rows S-20 through S-28.

## Engine — 2026-09-23

`UsableGameCalculator` and `StreamingPlanner` (Domain/Streaming) implement the
usable-game rule for both cadences, value summed over usable days, greedy over
decision days plus one improvement pass, the acquisition limit, IR and
rest-of-season drop protection, and deterministic ordering. A move's gain is the
change in the whole lineup's usable value from its day onward, which is the
contract's `SV(add) − SV(drop)` with slot competition included. With no stored
opponent defence and no injury feed, `MatchupAdjustment` is 1.0 and availability
1.0, each stated as plan evidence. User-marked protection is not built. Rows
S-20…S-28 are evidenced in `tests/FantasyBasketball.Domain.Tests/Streaming`.
