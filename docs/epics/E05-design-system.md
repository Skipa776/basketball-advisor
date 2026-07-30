# E05 — Design system and accessibility

**Requirements:** R19 · **Depends on:** E04 **and a `DESIGN.md` you produced**

The only epic with a human prerequisite. Structure is already specified; the
aesthetic is not, and an agent cannot choose it for you.

## Pre-flight — yours, not the agent's

Run steps 1–3 of
[`run_design_process.md`](../../context/codebase_okf/tasks/run_design_process.md)
before starting this epic:

1. `/design-consultation`, fed `DESIGN_BRIEF.md` → produces **`DESIGN.md`**
2. `/design-shotgun` on the **Draft Assistant** (the densest, most constrained
   surface — a system that survives it handles the rest)
3. `/plan-design-review` on the result

`DESIGN.md` must exist at the repo root before the prompt below is worth pasting.
Without it the agent has nothing to port and will invent an aesthetic you did not
choose — which is expensive to reverse once it is in fifteen components.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E05 in this repository: the design system and the accessibility gate.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R19),
   DESIGN.md (the aesthetic — produced by a human, canonical for look), and
   context/AGENT_CONTEXT_INDEX.md. Then read, in full:
     context/codebase_okf/contracts/design_system_contract.md
     context/codebase_okf/components/design_system.md
     context/codebase_okf/components/web_ui_blazor.md
     context/codebase_okf/tasks/run_design_process.md   (steps 4-7 are yours)
     context/codebase_okf/tests/test_matrix_ui_design.md
   Before writing any chart, load the `dataviz` skill.

2. Scope, in order:
   a. Tokens.razor.css — DESIGN.md's decisions become token VALUES and nothing
      else. Light and dark are both required, via prefers-color-scheme plus a
      persisted manual override.
   b. The component inventory from the contract, one component per entry, scoped
      CSS, tokens only. Components are presentational: they take data and render
      it, with no service or repository dependency.
   c. Rebuild every page from inventory components. No page defines its own
      styles. Every page gets a styled empty state and error state — a fresh
      self-hosted instance has no data, and that is the first screen a new user
      sees.
   d. Charts: category profile, trend sparkline, projection decomposition,
      back-test calibration. Series colours from tokens, never library defaults.
   e. The accessibility gate wired into scripts/gate.sh, scanning every page in
      both themes.

3. Required test rows: D-10 to D-21. Row D-14 is the one that matters most and is
   invisible to an accessibility scanner: record row offsets before and after a
   pick and assert NO REFLOW above the current scroll position. A board that
   shifts under the cursor when it re-ranks will cause a misclick on the wrong
   player during a live draft.

4. Do NOT paste generated HTML into .razor files. Static output from a design tool
   is a reference artifact; re-express its markup as Blazor components. Pasted
   markup introduces a second styling vocabulary next to the tokens, which is
   exactly what the token file exists to prevent.

   Do NOT change component structure to match a mockup. If a mockup needs a
   component the inventory lacks, the contract's inventory changes first.

5. Live-region discipline: announce the pick that was made and a change in the top
   recommendation. Do NOT announce every re-ranked row — an aria-live region over
   a fifty-row board that recomputes each pick is unusable with a screen reader,
   and adding it would make accessibility worse, not better.

6. Commit after each of a-e. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

7. Budget ladder: (a) tokens; (b) the components the Draft Assistant needs;
   (c) the Draft Assistant rebuilt; (d) remaining pages; (e) charts. A polished
   draft board beats six half-styled pages.

8. Non-negotiable: no hex colour, raw px spacing, or ms duration outside the token
   file; no CSS framework and no component library; risk and confidence are never
   conveyed by colour alone; accessibility_violations_allowed = 0 is a gate, not a
   target; never weaken the contract to pass it.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, the a11y scan result per page per
theme, the D-14 measurement, and the one next step. Under-claim rather than
over-claim.
```

---

## Exit gate

Rows D-10…D-21 green in both themes, zero accessibility violations on every page,
and a full draft entered with the keyboard only with no layout shift on re-rank.

## After the agent

Run steps 6–7 of the design process: `/design-review` on the running app (visual QA
with iterative fixes) and `/benchmark` on the Draft Assistant. Both run *after* the
a11y gate is green — they catch what it cannot.
