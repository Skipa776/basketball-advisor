# E03 — API, Blazor UI, and workers — MVP complete

**Requirements:** R1, R6 (and the display half of R8) · **Depends on:** E02
**Covers:** `AGENT_INSTRUCTIONS.md` steps 13–15

The surface. After this epic all ten MVP criteria pass and the app is usable by a
human for a real draft.

## Pre-flight

E02 landed green · `scripts/gate.sh` green · a mock draft runs through the
Application layer.

Build it **unstyled**. E05 brings the design system; a hand-styled UI here becomes
work to undo.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E03 in this repository: the HTTP API, the Blazor Server UI, and the
background workers. This epic completes the MVP.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (all of
   R1-R10 — this epic is where they are verified end to end), ARCHITECTURE.md,
   AGENT_INSTRUCTIONS.md steps 13-15, and context/AGENT_CONTEXT_INDEX.md. Then
   read, in full:
     context/codebase_okf/contracts/api_surface.md
     context/codebase_okf/components/api_host.md
     context/codebase_okf/components/web_ui_blazor.md
     context/codebase_okf/components/background_workers.md
     context/codebase_okf/safety/secrets_policy.md
     context/codebase_okf/tests/test_matrix_api_persistence.md

2. Scope, in order:
   a. API: every endpoint in ARCHITECTURE.md's MVP list, the single response
      envelope, exception middleware outermost, options binding with
      ValidateOnStart, structured logging with named holes.
   b. Blazor Server pages: Dashboard, My League, Players, Draft Assistant,
      Context Review, Data Sources. Components call Application services
      directly — this is in-process; do not route UI calls through HTTP.
   c. Workers: schedule, stats, and ADP refresh at their own cadences from
      configuration. One BackgroundService each, a scoped provider per run.
   d. Close out: integration tests, coverage minimums, statuses synced, validator
      clean.

3. Required test rows: A-01 to A-21. Rows A-11 (double-submit a pick) and A-20
   (projection decomposition displayed) are the two most likely to be skipped and
   the two that matter most — A-11 is the live-draft double-click, and A-20 is
   requirement R8's actual deliverable.

4. Build the UI unstyled: semantic HTML, no design decisions, no hand-rolled CSS
   beyond layout necessary to be usable. The design system arrives in E05 and will
   replace anything decorative you write here.

   Keyboard-first pick entry is functional, not decorative — implement it now:
   type to filter, arrow to select, enter to commit, focus returns to search.

5. Commit after each of a-d. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

6. Budget ladder: (a) the API; (b) Draft Assistant and Players pages; (c) the
   remaining pages; (d) workers. A usable draft board with three pages beats six
   half-wired pages.

7. Non-negotiable: pins only; no secret in any tracked file; every endpoint takes
   and forwards its CancellationToken; no exception escapes as a stack trace; a
   failed import returns 200 with a failed run, not 500; no placeholder pages for
   post-MVP features.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: which of the ten MVP criteria in PROJECT_REQUIREMENTS.md pass, with
the evidence for each; every command with its actual result and the SDK version;
anything missing. Under-claim rather than over-claim.
```

---

## Exit gate

**All ten MVP acceptance criteria pass**, rows A-01…A-21 green, and a human can
create a league, import data, run a draft with manual picks, and read a decomposed
projection entirely through the UI.
