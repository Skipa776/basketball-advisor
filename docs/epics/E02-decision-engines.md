# E02 — Projection, draft, and context engines

**Requirements:** R7, R8, R9, R10 · **Depends on:** E01
**Covers:** `AGENT_INSTRUCTIONS.md` steps 10–12

The decision engine itself: per-minute projections, the live draft board, and the
context engine that makes projections auditable. This is the epic the whole project
exists for.

## Pre-flight

E01 landed green · `scripts/gate.sh` green · at least one season of stats and one
ADP source imported, so the board has something to rank.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E02 in this repository: the projection, draft, and context engines.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R7-R10),
   ARCHITECTURE.md, AGENT_INSTRUCTIONS.md steps 10-12, and
   context/AGENT_CONTEXT_INDEX.md. Then read, in full:
     context/codebase_okf/contracts/projection_pipeline_contract.md
     context/codebase_okf/contracts/draft_value_contract.md
     context/codebase_okf/contracts/context_event_catalog.md
     context/codebase_okf/contracts/recommendation_evidence_contract.md
     context/codebase_okf/components/projection_engine.md
     context/codebase_okf/components/draft_engine.md
     context/codebase_okf/components/context_engine.md
     context/codebase_okf/safety/data_integrity_policy.md
     context/codebase_okf/tests/test_matrix_projection_draft.md
   The specs are canonical. Both the projection contract and the draft value
   contract contain worked examples and resolved contradictions — implement them
   exactly as written, including the resolutions.

2. Scope, in order:
   a. Projection engine: per-minute rates with league-average shrinkage, the
      minutes projector, projected games. ObservedStats and BaselineProjection
      persist as separate append-only records. The contract's worked example must
      reproduce to 4 decimal places.
   b. Draft engine: session, board, manual picks with undo, replacement level,
      positional scarcity, roster fit, market value, ranked recommendations with
      structured evidence. Replacement level recomputes from the available pool
      after every pick — never cached across picks.
   c. Context engine: ContextEvent and PlayerContextImpact CRUD, the human
      verification workflow, and a pure ContextApplier producing
      AdjustedProjection from a baseline.

3. Required test rows: P-01 to P-10, D-01 to D-09, C-01 to C-07, E-01 to E-05,
   plus the R7 latency assertion at realistic pool size.

4. Two invariants that are the point of this epic and are easy to violate:
   - Applying and then removing a context event must leave the BaselineProjection
     row byte-identical. Baselines are append-only with no update path.
   - VerificationState.Verified is assigned in exactly one place: the human
     verification endpoint. Nothing else may write it, now or ever.

5. Commit after each of a-c. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

6. Budget ladder: (a) projections; (b) the draft board; (c) the context engine.
   A working, tested board beats a half-built context engine.

7. Non-negotiable: pins only, nothing forbidden; domain code takes no repository
   and no clock; every recommendation carries at least one evidence item; no
   placeholder files; never weaken a safety concept or test to pass the gate.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated with state and next step, commit.

Final report: implemented / partial / missing, every command with its actual
result and the SDK version, any invariant you could not satisfy, and the one next
step. Under-claim rather than over-claim.
```

---

## Exit gate

Rows P-01…P-10, D-01…D-09, C-01…C-07, E-01…E-05 green, the latency assertion
passing, and a full mock draft completed end to end from manual pick entry with
every recommendation carrying evidence.
