# E07 — Category analyzer and draft intelligence

**Requirements:** R12, R13 · **Depends on:** E04

Category-league valuation and the draft intelligence that retires the MVP's
`MarketValue` proxy. Two epics' worth of math sharing one normal-distribution
helper, which is why they land together.

## Pre-flight

E04 landed green · `scripts/gate.sh` green · a category league configured, so the
analyzer has something to profile.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E07 in this repository: the category analyzer and draft intelligence.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R12,
   R13), ARCHITECTURE.md post-MVP section, and context/AGENT_CONTEXT_INDEX.md.
   Then read, in full:
     context/codebase_okf/contracts/category_value_contract.md
     context/codebase_okf/contracts/draft_intelligence_contract.md
     context/codebase_okf/contracts/draft_value_contract.md   (you will EDIT this)
     context/codebase_okf/components/category_analyzer.md
     context/codebase_okf/components/draft_intelligence.md
     context/codebase_okf/tests/test_matrix_advanced_decisions.md

2. Scope, in order:
   a. NormalDistribution — ONE Phi/erf implementation (Abramowitz-Stegun 7.1.26)
      shared by both halves of this epic, with one test against published values.
      Do not add MathNet.Numerics; it is on the forbidden list precisely because
      the whole need is one function.
   b. Category z-scores. Counting categories on projected weekly totals. RATIO
      categories are VOLUME WEIGHTED via the impact form — a player at 100% on two
      free-throw attempts must score near zero, not maximal. Pool percentages are
      aggregates, never means of player rates. TOV is inverted in exactly one
      place.
   c. TeamCategoryProfile and matchup win probability, computed on demand and
      never stored.
   d. Punt detection. PUNT_MAX is 3 because winning 9 categories requires 5, so
      punting k leaves 9-k from which 5 must come — feasible only while k <= 4,
      and 3 leaves a category of margin. The detector PROPOSES; a punt becomes
      active only when the user accepts it or sets their own.
   e. Survival probability: Normal(ADP, sigma) with sigma from the source or the
      documented fallback; positional runs shift the effective pick number rather
      than the distribution.
   f. Tier detection with a threshold DERIVED from the current pool's gap
      distribution, so it works in an 8-team and a 16-team league untuned.
   g. Category-aware draft value replacing ValueAboveReplacement with category
      marginal value. A category board is entirely in z-units; a points board
      entirely in points. Units never mix.

3. THE TERM REPLACEMENT — do this properly or not at all. OpportunityCost REPLACES
   MarketValue; it is not added alongside it. In the same commit:
     - edit contracts/draft_value_contract.md, swapping the MarketValue row and
       W_MARKET for OpportunityCost and W_OPPCOST
     - rename the DraftValue property
     - DELETE the MVP's valuePerPick calculation
     - update rows D-01 and D-05 in tests/test_matrix_projection_draft.md
   Two live definitions of "the market says wait" is exactly the drift the
   canonical-value rule exists to prevent. A rename that leaves the old term
   callable is not a replacement.

   Also delete the MVP's category-league banner and points-value fallback from the
   draft engine — with a real category board, keeping it means two live answers to
   the same question.

4. Required test rows: Y-01 to Y-08, Y-10 to Y-12, X-01 to X-07, plus the R7
   latency assertion with survival and tiering included. Row X-01 asserts Total
   equals the five addends WITH OpportunityCost and that no MarketValue term
   exists anywhere.

5. Commit after each of a-g, with the term replacement as its own commit. Never
   leave the tree broken at a commit boundary. Update OKF concept status in the
   same commit as its evidence. Commit locally.

6. Budget ladder: (a) NormalDistribution; (b) z-scores and profiles; (c) punt
   detection; (d) survival and the term replacement; (e) tiers; (f) category-aware
   board. Do not start the term replacement unless you can finish it in the same
   session.

7. Non-negotiable: exactly one Phi implementation; ratio categories are never
   summed and counting stats are never volume-weighted; profiles are computed, not
   stored; a points league never invokes the analyzer; no placeholder files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, whether the term replacement is
complete on both sides (code and contract), every command with its actual result,
and the one next step. Under-claim rather than over-claim.
```

---

## Exit gate

Rows Y-01…Y-12 and X-01…X-07 green, `MarketValue` absent from the entire repository
including the contract, and a category league producing a z-denominated board with a
detected punt proposal.
