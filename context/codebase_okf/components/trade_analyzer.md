---
type: component
title: Trade Analyzer
description: Before/after roster valuation, category win-delta, legality gating, and the scope of the verdict it will give.
tags: [component, trades, domain]
source_paths: [src/FantasyBasketball.Domain/Trades, src/FantasyBasketball.Application/Trades]
test_paths: [tests/FantasyBasketball.Domain.Tests/Trades]
depends_on: [../contracts/trade_contract.md, category_analyzer.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Valuation reuses the same starting-assignment code as the roster view.
  - No verdict is offered on the other side of a trade.
---

# Responsibility

Owns trade evaluation. The math, bands, and fairness boundary are owned by
[trade_contract](../contracts/trade_contract.md).

# Design

- The optimal-starting-assignment routine is **shared with
  [streaming_advisor](streaming_advisor.md)** — the same "which players can
  actually start" question, one implementation. A second copy would drift and
  produce a trade verdict inconsistent with the lineup the app recommends.
- `TradeEvaluationService` runs legality → points valuation → category profiles (in
  category leagues) → evidence. Legality first, so an impossible trade costs
  nothing to reject.
- Evaluation is a pure computation over a hypothetical roster. It never persists a
  roster change; a trade the user actually makes goes through the normal roster
  path.
- Multi-team trades resolve to the user's net send/receive set; the other legs are
  displayed, unevaluated.

# Invariants

- **The starting-assignment routine has exactly one implementation.** *Check: a grep
  asserting a single site, plus
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md) row
  R-08 asserting the trade view and the lineup view agree on the same roster.*
- **Legality is checked before valuation.** *Check: row R-03.*
- **Evaluation persists nothing.** *Check: row R-09 evaluates and asserts no roster
  row changed.*
- **Trading a player for himself is exactly neutral.** *Check: row R-05.*
- **Category leagues follow `ExpectedWinsDelta`.** *Check: row R-04.*
- **Exactly one verdict, for the user.** *Check: row R-06.*

# Change procedure

Changing valuation: the contract first, then this service, then rows R-01 … R-09.
Changing the starting-assignment routine touches both this and the streaming
advisor — same commit, both test sets.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md), rows
R-01 through R-09.
