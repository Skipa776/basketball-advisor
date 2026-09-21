---
type: task
title: Run Quality Gates
description: The commands and scans that must pass before any commit, and what each one is protecting.
tags: [task, gates, ci]
source_paths: [scripts/gate.sh]
test_paths: []
depends_on: [../tests/required_gates.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Owns the executable checks. Every invariant in this bundle that claims a check
is claiming one of these. Command strings and minimums are owned by
[`stack_config.toml`](../../../stack_config.toml).

# The gate

`scripts/gate.sh` runs these in order and stops at the first failure.

| # | Check | Protects |
|---|---|---|
| 1 | `python3 context/validate_okf.py` | The bundle itself: frontmatter, statuses, dates, links, index paths. Runs first because it needs nothing installed |
| 2 | `dotnet restore --locked-mode` | Reproducibility. Fails if the lock file and the pins disagree |
| 3 | Forbidden-package scan | The `[forbidden.packages]` list — licence traps and duplicate solutions |
| 4 | Forbidden-pattern scan | The `[forbidden.patterns]` regexes — `DateTime.Now`, `new HttpClient`, sync-over-async, empty catch, `float`/`double` for stats |
| 5 | Secret scan | Credentials in tracked files |
| 6 | `EnsureCreated` grep | Schema drift from the migration history |
| 7 | Canonical-value grep | The canonical-value rule (see below) |
| 8 | `dotnet format --verify-no-changes` | Formatting churn in diffs |
| 9 | `dotnet build -c Release` with zero warnings | Warnings-as-errors |
| 10 | `dotnet test -c Release` with coverage | The test matrices and the coverage minimums |

# The canonical-value grep

The check behind the rule that every canonical value lives in exactly one place.
It asserts that outside its owning file:

- no seed scoring literal (`1.2`, `1.5`, `3.0` in a scoring context) appears in
  `src/`;
- no projection constant (`500`, `0.85`, `20.0`) appears outside the options
  class;
- no draft weight literal appears in `src/`;
- no stat-name string literal (`"PTS"`, `"REB"`, …) appears outside `StatKey`
  and the source column maps;
- no provider name literal appears outside the canonical names list.

Values must be read from options or catalogs. A hit is a real finding: the
value has been duplicated and the two copies will drift.

# Coverage

Minimums are in `[gates]`: Domain 80%, Application 70%. Coverage is a floor, not
a target — a domain with 95% coverage and no test for the seed-league golden has
tested nothing that matters. The test matrices are what actually gate a commit.

# Running less than everything

While iterating, running steps 8–10 for a single project is fine. **The full
gate runs before every commit**, without exception — steps 1, 3, 4, 5, and 7 are
fast and are exactly the ones that catch problems no compiler will.

# When a gate fails

Fix the code. Do not weaken the gate, loosen a threshold, delete the assertion,
or add an exclusion — and never weaken a `stable_contract` safety concept to get
green. If a gate is genuinely wrong, say so and get it changed deliberately,
with the reasoning recorded in [assumptions](../assumptions.md).

# Verification

The gate is verified by its own use: it must be green at every commit boundary.

## React migration gate additions

The gate restores pinned npm packages and builds React before .NET static asset
discovery. Node 24 and Chromium are required. The real-cookie integration fixture
launches the Playwright/axe workspace test against its own throwaway database;
it does not contact live providers. `CHROME_PATH` can select a local installation.
The SSH.NET private test dependency pins the patched transitive version without
disabling NuGet vulnerability checks.
