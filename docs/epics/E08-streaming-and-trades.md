# E08 — Streaming advisor and trade analyzer

**Requirements:** R14, R15 · **Depends on:** E07

Both need the same two things — the category profile and an optimal starting-lineup
assignment — which is why they land together and share one implementation of each.

## Pre-flight

E07 landed green · `scripts/gate.sh` green · a schedule imported far enough forward
to plan against.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E08 in this repository: the streaming advisor and the trade analyzer.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R14,
   R15), ARCHITECTURE.md post-MVP section, and context/AGENT_CONTEXT_INDEX.md.
   Then read, in full:
     context/codebase_okf/contracts/streaming_contract.md
     context/codebase_okf/contracts/trade_contract.md
     context/codebase_okf/components/streaming_advisor.md
     context/codebase_okf/components/trade_analyzer.md
     context/codebase_okf/tests/test_matrix_advanced_decisions.md

2. Scope, in order:
   a. The optimal starting-lineup assignment — ONE implementation, shared by the
      streaming advisor and the trade analyzer. A second copy will drift and
      produce a trade verdict inconsistent with the lineup the app recommends.
      Order candidates by eligibility restriction ASCENDING, then value
      descending: a centre-only player must claim C before a UTIL-flexible star
      takes it.
   b. UsableGameCalculator — pure: (roster, schedule, lineupRules, horizon) ->
      UsableGame[]. Every entry carries a Reason INCLUDING the negative ones ("no
      open UTIL slot; contested by X") — that is the answer to the question the
      user is actually asking.
   c. Both lineup cadences. Weekly is not daily with a bigger horizon: for weekly,
      a started player's usable games are all his scheduled games and a benched
      player's are zero.
   d. StreamingValue: SUM per-day value across usable days. Do NOT multiply a
      summed value by the usable-game count — the design doc's formula does that
      and it double-counts. UsableGames is the cardinality of the sum, reported to
      the user, never a multiplier on it.
   e. StreamingPlanner: greedy over decision days plus ONE improvement pass, under
      the acquisition limit, with drop protection. Legality is checked INSIDE the
      planner so an illegal candidate is never scored and an illegal plan cannot
      be the argmax.
   f. Trade evaluation: before/after starting value with slot displacement and
      replacement backfill, category ExpectedWinsDelta, verdict bands, legality
      checked BEFORE valuation.

3. Two rules that are the point of these features:
   - A four-game week can score BELOW a three-game week when slots are the
     constraint. Row S-20 constructs exactly that case.
   - In a category league the verdict follows ExpectedWinsDelta, not raw value. A
     trade can raise value and lower expected wins by concentrating strength in
     categories already won; when they disagree, expected wins decides and the
     divergence becomes evidence.

4. The tool does NOT judge whether a trade is fair to the other manager. It has no
   model of their roster needs or punt strategy, and a verdict computed without one
   would be authoritative-looking noise. Multi-team trades produce exactly one
   verdict, for the user; other legs are displayed unevaluated.

5. Required test rows: S-20 to S-29, S-35, S-36, R-01 to R-09. Row R-05 trades a
   player for himself and asserts exact neutrality — the identity case that catches
   most valuation bugs. Row R-08 asserts the trade view and the lineup view agree
   on the same roster.

6. Commit after each of a-f. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

7. Budget ladder: (a) shared assignment; (b) usable games; (c) the planner;
   (d) points-league trades; (e) category trades. A correct usable-game calculator
   with no planner is still a shipped feature; a planner over a wrong calculator is
   not.

8. Non-negotiable: one starting-assignment implementation; plans are deterministic
   with ties broken by player id; no plan exceeds the acquisition limit, leaves the
   roster illegal, or drops a protected player; evaluation persists nothing; no
   placeholder files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, the S-20 and R-05 results, every
command with its actual result, and the one next step. Under-claim rather than
over-claim.
```

---

## Exit gate

Rows S-20…S-29, S-35, S-36, R-01…R-09 green, a multi-day streaming plan produced
under a real acquisition limit with each move explained, and a trade evaluated with a
before/after category profile.
