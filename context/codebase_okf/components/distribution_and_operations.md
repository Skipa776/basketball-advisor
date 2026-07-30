---
type: component
title: Distribution and Operations
description: The container image, compose quickstart, CI, seeded demo data, health endpoints, metrics, and the operator-facing docs.
tags: [component, deployment, observability, ci]
source_paths: [Dockerfile, compose.yaml, .github/workflows, README.md, src/FantasyBasketball.Infrastructure/Telemetry]
test_paths: [tests/FantasyBasketball.IntegrationTests/Configuration]
depends_on: [../safety/self_host_hardening.md, ../tasks/release_checklist.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - A stranger can clone, run one command, and see a working app with demo data.
  - Every gate that runs locally runs in CI on every push.
---

# Responsibility

Owns everything between "the code works" and "someone else can run it." Hardening
defaults are owned by
[self_host_hardening](../safety/self_host_hardening.md); the shipping steps by
[release_checklist](../tasks/release_checklist.md).

# The one-command test

The entire distribution story reduces to one thing:

```bash
git clone … && cd basketball-advisor && docker compose up
```

A stranger, no API keys, no .NET SDK on their machine, reaches a working app with
demo data loaded. **If that does not work, nothing else about "publishable" is
true.** It is a required integration test, not a README claim.

Demo data is a committed seed of fictional players and a fictional league, clearly
labelled as fictional in the UI. Real NBA data requires the operator's own
balldontlie key and a scrape they consented to.

# Container

- Multi-stage build: SDK image builds, runtime image ships. Chiselled/runtime-deps
  base, no SDK in the final image.
- Multi-arch (`linux/amd64`, `linux/arm64`) — a lot of self-hosting happens on ARM
  single-board machines and Apple silicon.
- Non-root, read-only root filesystem, no added capabilities.
- Published to `ghcr.io`, tagged by version; `latest` exists and the README pins a
  version.

# CI

GitHub Actions on every push and PR, running **the same `scripts/gate.sh` a
developer runs** — not a reimplementation. A CI pipeline that differs from the local
gate is two gates that will disagree at the worst moment.

Jobs: gate (validator, restore locked, scans, format, build, test with coverage),
image build for both architectures, and a compose smoke test that boots the stack
and hits `/health/ready`.

# Observability

- **Structured logs** via `ILogger` with named holes; correlation id per request.
- **`/health/live`** — process is up. **`/health/ready`** — database reachable and
  migrations applied. Kubernetes-shaped even though this ships as compose, because
  the semantics are what a reverse proxy and an operator want.
- **Metrics** (OpenTelemetry, Prometheus exposition): import success/failure by
  source, **scrape requests consumed against the rate ceiling**, recommendation
  latency, LLM tokens spent, pending identity matches.

Scrape-budget consumption is the metric that matters most: it is the operator's
early warning before a ban, and it is invisible without instrumentation.

`/metrics` is loopback-only by default
([self_host_hardening](../safety/self_host_hardening.md)).

# Docs

`README.md` (what it is, screenshots, quickstart, configuration table, upgrade,
backup, what the operator owns), `LICENSE` (MIT), `CONTRIBUTING.md`, and
`docs/` for the design doc, the OKF bundle, and back-test reports.

Screenshots are of demo data, never a real league — a real league screenshot leaks
someone's roster and dates the README the moment the season turns.

# Invariants

- **`docker compose up` from a clean clone reaches a working app.** *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-29 —
  the compose smoke test.*
- **CI runs the same gate script as local.** *Check: row A-30 asserts the workflow
  invokes `scripts/gate.sh`.*
- **The image is multi-arch, non-root, read-only rootfs, SDK-free.** *Check: rows
  A-24 and A-31.*
- **`/health/ready` fails when migrations are pending.** *Check: row A-32.*
- **Scrape-budget consumption is exported as a metric.** *Check: row A-33.*
- **Demo data is labelled fictional wherever it is displayed.** *Check: row A-26.*
- **README's configuration table matches the options classes.** *Check: row A-34
  reflects over options types and diffs against the table — documentation drift
  caught by a test.*

# Change procedure

Adding a config option: the options class, the README table, and row A-34 keeps them
honest. Changing the gate: `scripts/gate.sh` only — CI follows automatically.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows A-24,
A-26, A-29 through A-34.
