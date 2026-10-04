---
type: contract
title: Draft Simulation Contract
description: Monte Carlo lookahead for the user's pick — candidates, rollouts, common random numbers, opponent and user policies, lineup scoring, risk modes, and the latency budget.
tags: [contract, draft, simulation, statistics]
source_paths: [src/FantasyBasketball.Domain/Draft/DraftSimulator.cs, src/FantasyBasketball.Domain/Draft/LineupOptimizer.cs, src/FantasyBasketball.Domain/Schedule/SeasonSchedule.cs, src/FantasyBasketball.Application/Draft/DraftBoardService.cs]
test_paths: [tests/FantasyBasketball.Domain.Tests/Draft/DraftSimulatorTests.cs, tests/FantasyBasketball.Domain.Tests/Draft/LineupOptimizerTests.cs, tests/FantasyBasketball.Application.Tests/Surface/SurfaceQueryServiceTests.cs]
depends_on: [draft_value_contract.md, projection_pipeline_contract.md, model_params_contract.md]
status: implemented
last_updated: 2026-10-03
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Every candidate is judged by the user's final-roster season points over the same rollouts.
  - A board at the first pick of a 12-team, 13-round draft returns inside 2 s at p95.
  - The same draft state and seed always give the same board.
---

# Responsibility

Owns how the draft room decides between candidate picks once projections are
distributions ([projection_pipeline_contract](projection_pipeline_contract.md)).
It replaces the heuristic board's market and urgency nudges: opportunity cost is
read from simulated drafts, not priced by a formula
([draft_value_contract](draft_value_contract.md)).

# The simulation

For the best `K` available players by expected season value (default 15) and
`N` rollouts each (default 500):

1. The user takes the candidate at the current pick (or at their next pick, if
   the board is read while others are on the clock).
2. Other teams pick as the simulated opponents do: the active `opponent-choice`
   model's Plackett–Luce draw, else lowest ADP after `Normal(0, σ)` jitter with
   the starting-slot and bench-cap rules ([draft_value_contract](draft_value_contract.md) σ).
3. The user's later picks take the best expected value that fills an open
   starting slot, else the best value.
4. The final roster is scored by `LineupOptimizer` on the season's NBA schedule:
   every lineup period (day, or week for weekly leagues) the best players with
   games fill the starting slots — exact for this vertex-weighted matching.

Each player's value in rollout `n` is one draw: per-game points
`Normal(mean, sd)` (floored at 0) times the share of scheduled games played,
from the `BetaBinomial` games distribution. A player without a published
distribution contributes his point value with no spread.

**Common random numbers:** rollout `n` uses the same player draws and the same
opponent random stream for every candidate, so differences between candidates
are paired. *Check: row DS-02.*

# Reading the rollouts

| Field | Meaning |
|---|---|
| `Mean`, `Sd`, `P10`, `P90` | The user's final-roster season points across rollouts |
| `Score` | What the risk mode ranks by: `Mean`, `Mean − κ·Sd` (Cautious, κ = 0.5), or `P90` (Upside) |
| `Edge ± EdgeSe` | Mean paired difference to the top candidate and its standard error |
| `SurvivalToNextPick` | Share of the top candidate's rollouts (the runner-up's, for the top one) in which the player is still available at the user's next pick: the one after this when the user is on the clock, else the upcoming one, where the candidate would be taken (DS-07); null on the user's last pick |

# Seeding and latency

The seed is the draft id's first four bytes XOR `currentPick × 7919`, so a
refresh at the same pick returns the same board. *Check: row DS-01.* Rollouts
run in parallel with per-rollout seeds, so parallelism never changes results.
The p95 budget is 2 s at the first pick of a 12-team, 13-round draft with a
300-player pool and a 165-day schedule. *Check: row DS-06, which runs in the gate.*

# When it does not run

`GET /api/drafts/{id}/board/simulation` returns `board: null` and a reason when
the league is not a points league, the draft is complete, no candidate has a
published distribution, or no full NBA schedule (≥ 1,000 games) is imported for
the season ahead or a recent regular season. The heuristic board is always
available. *Check: row D-15.* Draft recommendations lead with the simulated
candidates, carrying edge and survival as evidence. *Check: row D-16.*
