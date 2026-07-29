---
type: schema
title: OKF Bundle Schema and Maintenance Contract
description: Frontmatter schema, status rules, and the canonical-value and same-commit rules that keep this bundle trustworthy.
tags: [meta, schema, maintenance]
source_paths: [context/codebase_okf, context/validate_okf.py]
test_paths: []
depends_on: []
status: planned
last_updated: 2026-07-29
owners: [engineering]
edit_policy: stable_contract
---

# Responsibility

Defines what a concept file must contain and the rules that stop this bundle
from drifting away from the code. `context/validate_okf.py` enforces the
mechanical half; the rest is enforced at review.

# Frontmatter schema

Required on every `.md` file under `codebase_okf/`:

| Key | Rule |
|---|---|
| `type` | `component`, `contract`, `task`, `safety_policy`, `test_matrix`, `test_policy`, `assumptions`, `schema`, `context_index` |
| `title` | Human-readable |
| `description` | One line, used for routing decisions — make it earn its place |
| `tags` | Inline list, two or three |
| `source_paths` | Repo-relative; forward-looking paths allowed while `planned` |
| `test_paths` | Repo-relative test files |
| `depends_on` | Inline list of relative paths that **must resolve** |
| `status` | `planned` \| `partial` \| `implemented` |
| `last_updated` | `YYYY-MM-DD` |

Optional: `owners`, `risk_level` (`low`/`medium`/`high`), `edit_policy`
(`normal`/`stable_contract`), `done_criteria`.

# Invariants

- **Status discipline.** `planned → partial → implemented` is promoted only with
  code *and* passing tests as evidence, in the same commit as that evidence.
  Documentation never promotes a status. *Checked at review; over-claiming is
  the single strongest signal that this bundle has stopped being trustworthy.*
- **Canonical-value rule.** Every stat key, scoring number, enum member, weight
  constant, threshold, and ID lives in exactly one concept. Everywhere else
  links to it. *Checked by the canonical-value grep in
  [run_quality_gates](tasks/run_quality_gates.md).*
- **Same-commit rule.** Code, contract, path, or test changes update their
  affected concepts in the same logical commit.
- **Concepts link, never restate.** Upstream specs
  ([ARCHITECTURE](../../ARCHITECTURE.md),
  [PROJECT_REQUIREMENTS](../../PROJECT_REQUIREMENTS.md),
  [stack_config.toml](../../stack_config.toml), the
  [design doc](../../docs/design/fantasy-basketball-decision-engine.md)) are
  referenced, not copied. Restatement is where drift starts.
- **`edit_policy: stable_contract` concepts may not be weakened to make a gate
  pass.** They require explicit user approval to change.
- **Every new concept is linked from [index.md](index.md) and from the task
  router**, and names its verification path. *Checked by the validator's
  link and index-path resolution.*
- **Every invariant names its check.** If a rule in any concept has no test,
  scan, or validator check, it is prose — say so out loud in
  [assumptions.md](assumptions.md) rather than pretending otherwise.

# Change procedure

Adding a concept: create the file with full frontmatter, link it from
[index.md](index.md), add a router row to
[`AGENT_CONTEXT_INDEX.md`](../AGENT_CONTEXT_INDEX.md), run the validator.

Changing a canonical value: edit the owning concept, then update every artifact
that consumes it (fixtures, goldens, parser maps, migrations) in the same commit.

# Verification

```bash
python3 context/validate_okf.py
```

Exit 0 required. It checks frontmatter presence and required keys, the status
enum, date format, `depends_on` resolution, relative-link resolution, and that
every backticked bundle path in the task router resolves. It is stdlib-only and
runs before any dependency is installed — including before the .NET SDK exists.
