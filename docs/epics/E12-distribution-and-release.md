# E12 — Distribution, observability, and release

**Requirements:** R21, R22 · **Depends on:** E05, E08, E09, E10, E11

The epic that makes it publishable. Everything between "the code works" and "a
stranger can run it."

## Pre-flight

Every prior epic landed green · `scripts/gate.sh` green · concept statuses honest.

The single test that matters: a stranger clones, runs one command, and sees a working
app. If that fails, nothing else about "publishable" is true.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E12 in this repository: distribution, observability, and release.

1. Read in order: AGENTS.md, stack_config.toml ([distribution] section),
   PROJECT_REQUIREMENTS.md (R21, R22), and context/AGENT_CONTEXT_INDEX.md. Then
   read, in full:
     context/codebase_okf/safety/self_host_hardening.md      (read this FIRST)
     context/codebase_okf/components/distribution_and_operations.md
     context/codebase_okf/tasks/release_checklist.md
     context/codebase_okf/safety/secrets_policy.md
     context/codebase_okf/tests/test_matrix_api_persistence.md (rows A-22 to A-34)
   Add the two OpenTelemetry packages from stack_config.toml [dependencies.epic].

2. Scope, in order:
   a. Dockerfile: multi-stage, SDK builds and runtime ships, no SDK in the final
      image. Multi-arch (linux/amd64, linux/arm64) — a lot of self-hosting happens
      on ARM boards and Apple silicon. Non-root user, read-only root filesystem,
      no added capabilities.
   b. compose.yaml for the quickstart: app plus Postgres 17, Postgres port NOT
      published to the host, a generated-on-first-run database password rather than
      a hardcoded one, and a comment saying it is not for anything network-facing.
   c. Demo data: a committed seed of FICTIONAL players and a fictional league,
      opt-in via flag, labelled fictional everywhere it appears in the UI. Real NBA
      data needs the operator's own key and a scrape they consented to.
   d. Health endpoints: /health/live (process up) and /health/ready (database
      reachable AND migrations applied).
   e. Metrics via OpenTelemetry: import success/failure by source, RECOMMENDATION
      LATENCY, LLM tokens spent, pending identity matches, and — most
      importantly — SCRAPE REQUESTS CONSUMED AGAINST THE RATE CEILING. That last
      one is the operator's early warning before a ban and is invisible without
      instrumentation. /metrics is loopback-only by default.
   f. CI: .github/workflows/ci.yml already exists and runs the bundle validator
      plus scripts/gate.sh. EXTEND it — add the multi-arch image build and a
      compose smoke test that boots the stack and hits /health/ready. Do not
      reimplement the gate steps inline: a CI pipeline that differs from
      scripts/gate.sh is two gates that will disagree at the worst moment.
   g. README.md: what it is, screenshots (DEMO DATA ONLY — a real league screenshot
      leaks someone's roster and dates the README the moment the season turns),
      quickstart, a configuration table, upgrade, backup with pg_dump, and an
      explicit "what the operator owns" section. LICENSE (MIT), CONTRIBUTING.md,
      CHANGELOG.md.
   h. Publish to ghcr.io, tagged by version. README pins a version, not latest;
      latest moves last.

3. Hardening defaults that must hold with the operator doing nothing:
   - Bind loopback. Exposing it is an explicit edit.
   - HTTPS required outside Development; a non-loopback bind with no TLS FAILS AT
     BOOT with a named error.
   - NO DEFAULT CREDENTIAL ANYWHERE. Every self-hosted breach story starts with a
     credential that shipped in the artifact. The instance is claimed by whoever
     registers first, and that window is the operator's to close.
   - Registration closed after the owner claims the instance.
   - LLM disabled; the app is complete without a key.

4. Required test rows: A-22 to A-34. Two of them are the epic:
   - A-29: docker compose up from a CLEAN CLONE with no keys reaches a working app
     with demo data and a healthy /health/ready.
   - A-34: the README configuration table matches the options classes BY
     REFLECTION. A README that documents an intention rather than the artifact is
     the most common defect in self-hosted software, and it is trivially checkable.

5. Migrations must apply forward on a POPULATED volume with no data loss. A
   migration that cannot preserve data fails and says what to back up first.

6. Commit after each of a-h. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally; do
   not push to a container registry without explicit confirmation.

7. Budget ladder: (a) Dockerfile + compose + the A-29 smoke test; (b) health and
   metrics; (c) CI; (d) README and docs; (e) publish. A working quickstart with no
   README is more useful than a README with no working quickstart.

8. Non-negotiable: no default credential in any shipped artifact; secret scan clean
   across compose.yaml, Dockerfile, and every appsettings file; /metrics not
   reachable off-host by default; screenshots use demo data only; no placeholder
   files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated, commit.

Final report: implemented / partial / missing, the A-29 result verbatim, the image
size and architectures, every command with its actual result, and the one next step.
Under-claim rather than over-claim.
```

---

## Exit gate

Rows A-22…A-34 green, and **you personally** clone into an empty directory, follow
the README's own instructions literally, and reach a working app. That last step is
the only one that catches a README documenting an intention, and it is the one most
likely to be skipped.

Then walk
[`release_checklist.md`](../../context/codebase_okf/tasks/release_checklist.md)
top to bottom before tagging.
