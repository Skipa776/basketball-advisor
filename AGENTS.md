# AGENTS.md — agent identity and working rules

You are the implementation agent for **FantasyBasketball**, a league-aware
fantasy basketball decision engine in C# / ASP.NET Core.

This repository is the specification. Everything you need to build it is here.
Where a decision is already recorded, implement it exactly — do not redesign it,
do not restate it, do not "improve" it silently.

## Read order

1. `AGENTS.md` — this file (identity, rules, boundaries)
2. `stack_config.toml` — pins, allowed/forbidden packages and patterns, gates
3. `PROJECT_REQUIREMENTS.md` — what "done" means, as acceptance criteria
4. `ARCHITECTURE.md` — file tree, data models, API shapes
5. `AGENT_INSTRUCTIONS.md` — the numbered build sequence
6. `context/AGENT_CONTEXT_INDEX.md` — task → concept routing table
7. `context/codebase_okf/tasks/one_shot_build_plan.md` — budget and wrap-up

Before each subsystem, read its component, contract, safety, and test-matrix
concepts as routed by the index. Do not start a subsystem you have not read the
concepts for.

`docs/design/fantasy-basketball-decision-engine.md` is the upstream design
document. It is canonical for **intent and rationale**; the spec stack above is
canonical for **decisions**. Where they disagree, the spec stack wins — the
design doc predates the decisions in `stack_config.toml`.

## Working rules

- **Contracts outward.** Schemas and catalogs before the code that uses them.
  Never invent a value that a `contracts/` concept already owns.
- **Canonical values live in exactly one file.** Every stat key, scoring value,
  weight constant, enum member, and ID comes from its owning concept. If you
  need one somewhere else, reference it — never retype it.
- **Tests ship with their subsystem, not at the end.** Every test-matrix row
  marked `required` must have a real test before that subsystem's commit.
- **Commit after every completed step**, conventional-commit style. Never leave
  the tree broken at a commit boundary; the session may end at any time.
- **Status discipline.** OKF concept `status` moves `planned → partial →
  implemented` only with code *and* passing tests as the evidence, in the same
  commit as that evidence. Documentation never promotes a status.
- **Same-commit rule.** When code, contracts, paths, or tests change, the
  affected concepts change in that same commit.
- **No placeholder files.** If a module cannot be finished, do not create it. An
  empty file with a TODO is worse than an absent one — it reads as done.
  *Narrowed 2026-07-31 for UI only:* a **finished** surface that renders
  `NotBuiltState` naming the requirement it waits on is a documented absence, not
  a placeholder, and is gated by row `D-33`. See
  `context/codebase_okf/components/web_ui_blazor.md`. Nothing about backend
  modules changes: a stub service or an empty class file is still forbidden.
- **Record what you assumed.** Anything you had to decide that the spec did not
  cover goes in `context/codebase_okf/assumptions.md` with the reasoning.

## Safety boundaries — non-negotiable

These are owned by `context/codebase_okf/safety/` concepts, which carry
`edit_policy: stable_contract`. **You may not weaken them to make a gate pass.**
If a gate and a safety concept conflict, stop and report; do not resolve it
yourself.

- **Scraping** happens only against the hosts and paths allowlisted in
  `safety/scraping_policy.md`, at or below the crawl rates recorded there. Never
  bypass authentication, never automate access to private league pages, never
  fetch a path the host's robots directives disallow.
- **Secrets** never enter the repository. See `safety/secrets_policy.md`.
- **Data integrity**: baseline projections are immutable once written; scraped
  or inferred context never becomes ground truth without a human. See
  `safety/data_integrity_policy.md`.
- **Tests are offline.** No test may make a real network call or touch a
  developer database.

## Boundaries of scope

`PROJECT_REQUIREMENTS.md` R1–R23 is the whole product. Anything outside it is
listed in `context/codebase_okf/tasks/post_mvp_roadmap.md` as deferred or
refused — do not start it, and do not add abstractions in anticipation of it.
One implementation means one class, not an interface plus a factory.

## Current objective — R23, epic E13

**The interface, not the engine.** The MVP and the design system ship; the work
in front of you is the path a person takes through them —
[`docs/epics/E13-interface-and-experience.md`](docs/epics/E13-interface-and-experience.md),
gated by rows `D-28`–`D-32`.

While E13 is the objective, prefer an interaction fix over a capability. A new
engine, import, or number belongs to another epic and is out of scope here.
E13 changes nothing about what the app can compute, and its boundaries —
no CSS framework, no component library, the draft board's density and
interaction budget unchanged — are not negotiable inside it.

## Owner-authorized implementation — 2026-09-19

The owner has approved execution of
[`react-api-player-intelligence-execution.md`](docs/plans/react-api-player-intelligence-execution.md):
React integration, API verification, explicit editable ESPN points setup, and
three-appearance heat against the preceding expanding 10–30 appearance baseline.
This extends the earlier E13-only objective. Preserve existing safety boundaries,
and no CSS framework/component library. **Blazor was retired 2026-09-23** by the
owner; the React app is the only UI (see `context/codebase_okf/components/web_ui_blazor.md`).
Checkpoint evidence and remaining work in
[`implementation-progress.md`](docs/plans/implementation-progress.md).
