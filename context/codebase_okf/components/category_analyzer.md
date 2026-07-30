---
type: component
title: Category Analyzer
description: Z-scores, team category profiles, matchup outlook, punt detection, and the one shared normal-distribution helper.
tags: [component, categories, domain]
source_paths: [src/FantasyBasketball.Domain/Categories, src/FantasyBasketball.Application/Categories]
test_paths: [tests/FantasyBasketball.Domain.Tests/Categories]
depends_on: [../contracts/category_value_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - One normal-distribution implementation exists, with one test.
  - The MVP's category-league fallback banner is deleted when this lands.
---

# Responsibility

Owns category-league valuation and comparison. Math, weighting, and punt rules are
owned by [category_value_contract](../contracts/category_value_contract.md).

# Design

- `CategoryZScore` computation is pure and takes the pool as a parameter — the pool
  definition is a policy decision that belongs to the caller, and passing it in is
  what makes punt-adjusted recomputation cheap.
- `NormalDistribution` is the **single** `Φ`/`erf` implementation in the codebase,
  shared with [draft_intelligence](draft_intelligence.md). One function, one test
  against published values, no numerics package.
- `TeamCategoryProfile` is computed on demand from the current roster. Like trend
  rankings, it is never stored — a roster change invalidates it immediately.
- `PuntDetector` proposes; it never applies. A punt set becomes active only when the
  user accepts it or sets their own, and `UserChosen` records which.
- The MVP's category-league draft banner and points-value fallback
  ([draft_engine](draft_engine.md)) are **deleted** in the commit that lands this —
  leaving both would mean two live answers to what a category board shows.

# Invariants

- **Exactly one `Φ` implementation exists.** *Check: a grep for `erf` /
  cumulative-normal implementations asserting a single site.*
- **Z-score computation is pure and pool-parameterized.** *Check: its signature.*
- **Profiles are computed, never stored.** *Check:
  [test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md) row
  Y-10 asserts a roster change moves the profile with no invalidation step.*
- **Punt detection proposes only.** *Check: row Y-11 asserts no punt is active
  until accepted.*
- **A points league never invokes the analyzer.** *Check: row Y-12.*
- **Ratio and counting categories go through their own paths** — a counting stat can
  never be volume-weighted and a ratio can never be summed. *Check: rows Y-01, Y-03.*

# Change procedure

Changing the pool definition, weight bands, or punt thresholds: the contract first,
then this component, then rows Y-01 … Y-12.

# Verification

[test_matrix_advanced_decisions](../tests/test_matrix_advanced_decisions.md), rows
Y-01 through Y-12.
