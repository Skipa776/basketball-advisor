# One-shot build prompt

Paste the block below into a **fresh** agent session at the repository root.
The repo is the spec — this prompt supplies only what the repo cannot know
about the session.

**Pre-flight, before you paste it:**

- `dotnet --list-sdks` shows a 10.0.x SDK. *(It was absent on this machine as of
  2026-07-29 — install it first.)*
- `docker info` succeeds.
- `python3 context/validate_okf.py` exits 0.

---

```text
You are explicitly authorized to implement: create and edit files, run
commands, and commit. Any planning-first restriction in CLAUDE.md is lifted for
this session. You are the primary implementation agent — proceed without
waiting for further authorization, and do not ask for approval between steps.

Build FantasyBasketball in this repository, end to end, in this session.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md,
   ARCHITECTURE.md, AGENT_INSTRUCTIONS.md, context/AGENT_CONTEXT_INDEX.md, then
   context/codebase_okf/tasks/one_shot_build_plan.md. Before each subsystem,
   read its component, contract, safety, and test-matrix concepts as routed by
   the index. The specs are canonical — do not restate them, do not redesign
   them. Where a decision is already recorded, implement it exactly as written.

2. Follow the numbered build sequence in AGENT_INSTRUCTIONS.md. Write each
   subsystem's tests with the subsystem, not at the end. Every test-matrix row
   marked required must have a real test before that subsystem's commit.

3. Commit after every completed step with a conventional-commit message. Never
   leave the tree broken at a commit boundary — the session may end at any
   time. Update OKF concept status fields in the same commit as the code and
   tests that justify them. Commit locally; do not push.

4. Any cross-agent review gate is waived for this run: self-review each diff
   against the safety concepts and the forbidden patterns in stack_config.toml
   instead.

5. Budget discipline — if you cannot finish everything, protect this order and
   never reorder it:
   (a) skeleton, gates, stat vocabulary, scoring engine with goldens;
   (b) persistence, identity, ingestion pipeline, balldontlie, both scrapers;
   (c) projection engine and a working, re-ranking draft board;
   (d) context engine;
   (e) API, Blazor UI, workers, polish.
   UI breadth is the first thing to cut. A working, tested draft board beats a
   broad, broken surface.

6. Wrap-up protocol — at roughly 10% of budget remaining, stop starting new
   subsystems. Make what exists green (dotnet format, build with zero warnings,
   all tests passing), set every OKF concept status to match reality, run
   python3 context/validate_okf.py, append a "current state and next steps"
   note to context/codebase_okf/assumptions.md, and commit.

7. Non-negotiable at any budget: only the packages and exact pins in
   stack_config.toml; nothing from its forbidden list; tests run offline with
   fixture-backed fakes and throwaway containers, never a developer database;
   scraping stays inside the allowlist in
   context/codebase_okf/safety/scraping_policy.md; no placeholder or TODO-only
   files — if a module cannot be finished, do not create it; never weaken a
   safety concept or a test to make a gate pass.

Final report: what is implemented, partial, and missing; every command you ran
with its actual result and the toolchain version used; any invariant you could
not satisfy and why; and the single next step a resuming agent should take.
Under-claim rather than over-claim.
```

---

## After the run

Grade the **context**, not just the code:

1. Re-run every claimed gate yourself; note the SDK version actually used.
2. Verify from clean: migrations from empty, `dotnet restore --locked-mode`.
3. Attack the lifecycle invariants directly — double-submit a pick, undo a
   stale pick, verify an already-verified event, mutate a baseline.
4. Diff artifacts against the catalogs: stat keys, seed values, event types,
   provider names, weight constants.
5. Run the forbidden-pattern, secret, and canonical-value scans.
6. Check the agent's self-report against what you found. Over-claiming means
   status discipline failed and the bundle can no longer be trusted at face
   value.
7. **Root-cause each defect to its context gap**: was the violated invariant
   executable or prose? Add the missing test-matrix row, validator check, or
   catalog entry — and update the affected concepts in the same commit as the
   fix. That loop is what makes this format compound.
