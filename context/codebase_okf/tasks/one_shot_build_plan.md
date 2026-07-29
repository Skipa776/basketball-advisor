---
type: task
title: One-Shot Build Plan
description: Budget ladder, commit discipline, and the wrap-up protocol for an unattended end-to-end build.
tags: [task, build, budget]
source_paths: []
test_paths: []
depends_on: [run_quality_gates.md, ../assumptions.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Owns how an unattended build session spends its budget and how it lands safely
when it runs out. The build *sequence* is owned by
[`AGENT_INSTRUCTIONS.md`](../../../AGENT_INSTRUCTIONS.md); this concept owns
budget policy and the exit protocol.

# Before starting

- **The .NET 10 SDK must be installed.** It was absent as of 2026-07-29 — see
  [assumptions](../assumptions.md). Verify with `dotnet --list-sdks` first;
  everything downstream fails confusingly without it.
- Docker must be running for Postgres and Testcontainers.
- `python3 context/validate_okf.py` should be clean before you start, so any
  later failure is yours and not inherited.

# Commit discipline

- One commit per completed step, conventional-commit style
  (`feat(scoring): points engine with seed-league goldens`).
- **Never leave the tree broken at a commit boundary.** The session can end at
  any moment; every commit must build and pass its tests.
- Update OKF concept `status` in the **same commit** as the code and tests that
  justify it. `planned → partial` when the code exists but the required test
  rows do not all pass; `partial → implemented` only when they do.
- Do not push. Commit locally unless a remote is configured and pushing was
  asked for.

# Budget ladder

If you cannot finish everything, sacrifice from the bottom. **Never reorder
this.**

1. **Steps 1–3** — skeleton, gates, stat vocabulary, scoring engine with
   goldens. This is the project's floor: a tested domain service that computes
   league-specific fantasy value. A session that delivers only this has
   delivered the design doc's stated first milestone.
2. **Steps 4–9** — persistence, identity, ingestion pipeline, balldontlie,
   both scrapers. This is where the app stops being a calculator.
3. **Steps 10–11** — projections and a working, re-ranking draft board.
4. **Step 12** — the context engine.
5. **Steps 13–15** — UI breadth, workers, polish.

UI breadth is the first thing to cut. Tests for code that exists are the last —
untested code that ships is worse than absent code, because it gets trusted.

If a step cannot be finished, **do not leave it half-built**. Revert to the last
green commit for that subsystem and record the attempt in
[assumptions](../assumptions.md). No placeholder files, no TODO-only classes.

# Wrap-up protocol

At roughly **10% of budget remaining**, stop starting new subsystems and:

1. Make what exists green: `dotnet format`, build with zero warnings, all tests
   passing.
2. Set every concept `status` to match reality. **Under-claim rather than
   over-claim** — an honest `partial` is useful, a false `implemented` poisons
   every future session that trusts this bundle.
3. Run `python3 context/validate_okf.py` and fix what it reports.
4. Append a "current state and next steps" section to
   [assumptions](../assumptions.md): what works, what does not, the single next
   action, and any decision you had to invent.
5. Commit.

# Final report

Report, honestly:

- what is implemented, partial, and missing;
- every command run, its actual result, and the toolchain version used —
  "tests pass" on an unstated interpreter is not a result;
- any invariant you could not satisfy and why;
- the single next step for a resuming agent.

Over-claiming in this report is the failure mode that makes the whole format
worthless. Under-claiming costs nothing.
