---
type: task
title: Release Checklist
description: What must be true before publishing a version, in the order that catches problems cheapest.
tags: [task, release, distribution]
source_paths: [README.md, LICENSE, Dockerfile, compose.yaml, .github/workflows]
test_paths: [tests/FantasyBasketball.IntegrationTests/Configuration]
depends_on: [../components/distribution_and_operations.md, ../safety/self_host_hardening.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

The gate between "it works on my machine" and "someone else can run it." Ordered so
the cheap checks fail first.

# Code and bundle

- [ ] `scripts/gate.sh` green — including the OKF validator, both scans, the
      canonical-value grep, coverage minimums, and the a11y scan.
- [ ] Every concept `status` matches reality. **Under-claim rather than over-claim** —
      a false `implemented` poisons every future session that trusts this bundle.
- [ ] `assumptions.md` current: prose-only invariants still listed as such, resolved
      ones cited to their evidence.
- [ ] No `TODO`, no commented-out code, no placeholder page in a shipped route.

# Security

- [ ] Secret scan clean across `src/`, `compose.yaml`, `Dockerfile`, and every
      `appsettings*.json`.
- [ ] **No default credential in any shipped artifact.** The single most common
      self-hosted breach, and the cheapest to check.
- [ ] Tenant isolation sweep green; `IgnoreQueryFilters` absent from `src/`.
- [ ] Non-loopback bind without TLS fails at boot.
- [ ] Registration closes after the owner claims the instance.
- [ ] `/metrics` not reachable off-host by default.
- [ ] LLM disabled by default; the app is complete without a key.

# Compliance

- [ ] **Robots directives re-verified** for every scrape host, dates updated in
      [scraping_policy](../safety/scraping_policy.md). Verification older than 90
      days is not verification.
- [ ] Rate ceiling unchanged and still far below every host's threshold.
- [ ] No authenticated or private-page scrape anywhere.
- [ ] Terms of use for each host read by a human this release
      ([assumptions](../assumptions.md) — not machine-checked, and the release is
      when someone actually owns it).

# Distribution

- [ ] `docker compose up` from a **clean clone in an empty directory** reaches a
      working app with demo data, no keys configured. Run it for real; do not trust
      the test alone for a release.
- [ ] Multi-arch image builds and both architectures boot.
- [ ] Migrations apply forward on a **populated** volume with no data loss.
- [ ] `pg_dump` backup and restore verified against the release image.

# Docs

- [ ] README: what it is, screenshots (demo data only — never a real roster),
      quickstart, configuration table, upgrade, backup, what the operator owns.
- [ ] Configuration table matches the options classes — row A-34 proves it.
- [ ] LICENSE present; every dependency's licence compatible. **Re-check this
      release**: the `[forbidden.packages]` list exists because several .NET packages
      changed licence mid-life.
- [ ] CHANGELOG entry with the version and any breaking configuration change.
- [ ] Back-test report committed if the projection or draft math changed this
      release — fitted numbers are a claim, and the claim needs its evidence.

# Publish

- [ ] Version tagged; the image tagged to match. `latest` moves last.
- [ ] README pins the version, not `latest`.
- [ ] Release notes name every breaking configuration change explicitly.

# After

- [ ] Pull the published image into an empty directory and run the quickstart from
      the README's own instructions, following them literally. This is the only step
      that catches a README that documents an intention rather than the artifact —
      and it is the one most likely to be skipped.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) rows A-22
through A-34 cover the automatable items. The clean-clone run and the terms-of-use
read are human steps on purpose.
