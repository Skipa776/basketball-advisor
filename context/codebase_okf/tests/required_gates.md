---
type: test_policy
title: Required Gates and Test Policy
description: What must be tested before a subsystem commits, the row-ID convention, and the isolation rules every test obeys.
tags: [tests, policy, gates]
source_paths: []
test_paths: [tests]
depends_on: [../tasks/run_quality_gates.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Owns test policy: what counts as tested, how rows are identified, and the
isolation rules. The commands are owned by
[run_quality_gates](../tasks/run_quality_gates.md).

# The rule that makes matrices real

Every row marked **required** must have a corresponding test **before its
subsystem's commit**. A test matrix with no gate is decoration; this is the
gate. [`AGENT_INSTRUCTIONS.md`](../../../AGENT_INSTRUCTIONS.md) states each
step's completion criteria in these terms.

Rows marked *optional* are worth having and may be sacrificed under budget
pressure. Required rows may not.

# Row IDs

Each ID appears in exactly one matrix; the prefix says which subsystem, and
every invariant in this bundle cites the row that would catch its violation.

| Prefix | Range | Matrix |
|---|---|---|
| `S-01`–`S-08` | scoring | [test_matrix_scoring](test_matrix_scoring.md) |
| `P-`, `D-`, `C-`, `E-` | projection, draft, context, evidence | [test_matrix_projection_draft](test_matrix_projection_draft.md) |
| `I-`, `N-`, `S-10`–`S-14`, `W-` | ingestion, identity, scrapers, workers | [test_matrix_ingestion_scrapers](test_matrix_ingestion_scrapers.md) |
| `A-` | architecture, persistence, API | [test_matrix_api_persistence](test_matrix_api_persistence.md) |

Adding an invariant to any concept means adding its row here, in the same
commit. An invariant with no row is prose — and prose belongs in
[assumptions](../assumptions.md) as knowingly unenforced, not disguised as a
guarantee.

# Isolation rules

- **Domain tests**: no database, no network, no file I/O, no clock. Pure inputs
  and outputs.
- **Application tests**: hand-written fakes only. No mocking framework
  (`Moq` is forbidden), no containers.
- **Integration tests**: a Testcontainers Postgres per class fixture, and an
  HTTP handler that **throws on any real send**. A test that reaches the
  internet is a broken test even when it passes — it will fail in a tunnel, in
  CI, and on draft night.
- **No test writes to a developer database.** Connection strings are generated
  by the container, never read from developer configuration.
- **Determinism**: time is injected, ordering is asserted explicitly, no
  `Random` without a fixed seed. A flaky test gets fixed or deleted, never
  retried.

# Coverage

Domain 80%, Application 70%, from `[gates]` in
[`stack_config.toml`](../../../stack_config.toml). Coverage is a floor. The
matrices are the real bar — 95% coverage without the seed-league golden means
nothing is protected.

# Verification

`dotnet test` plus the coverage collector, run by `scripts/gate.sh`.
