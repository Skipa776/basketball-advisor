---
type: component
title: League Import Adapters
description: Yahoo OAuth, Sleeper read-only, and CSV import behind one snapshot interface, with token custody and the diff-and-confirm flow.
tags: [component, providers, import]
source_paths: [src/FantasyBasketball.Infrastructure/Providers, src/FantasyBasketball.Infrastructure/Import]
test_paths: [tests/FantasyBasketball.IntegrationTests/Providers]
depends_on: [../contracts/league_import_contract.md, ../safety/secrets_policy.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - Every adapter is optional; the app is fully usable with all of them removed.
  - Refresh tokens are encrypted at rest and absent from every log line.
---

# Responsibility

Owns the provider adapters. Mapping rules, the fail-loud rule, and the
non-destructive flow are owned by
[league_import_contract](../contracts/league_import_contract.md).

# Design

- All three implement `IFantasyLeagueProvider` and return
  `FantasyLeagueSnapshot`. **Nothing downstream can tell them apart** — that is the
  test of whether the abstraction is real.
- **Yahoo**: authorization-code flow with refresh. Tokens are encrypted with ASP.NET
  Data Protection, stored per user (owned data —
  [tenancy_policy](../safety/tenancy_policy.md)), and redacted from logs. A refresh
  failure marks the connection stale and prompts a reconnect; it never retries into
  a provider lockout.
- **Sleeper**: no auth, read-only. Each endpoint is gated on a committed NBA fixture;
  the adapter's supported-endpoint list is **derived from the fixture set**, so an
  unvalidated endpoint is structurally uncallable rather than merely undocumented.
- **CSV**: a documented column format with a downloadable template. The template and
  the parser are generated from one schema definition so they cannot disagree.
- Import runs through the standard ingestion pipeline
  ([ingestion_pipeline](ingestion_pipeline.md)) — same rate limiting, same
  `DataImportRun`, same provenance.

# Optionality is the requirement

Each adapter registers only when configured. With none configured, the Import page
offers CSV and manual entry, and every other feature works. This is not graceful
degradation — it is the design doc's central rule, and the test that proves it runs
the entire MVP flow with every adapter unregistered.

# Invariants

- **Nothing downstream branches on provider.** *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md) row
  L-09 asserts no `switch` or `if` on a provider name outside the adapter
  assemblies.*
- **All adapters removable; app still fully usable.** *Check: row L-04.*
- **Tokens encrypted at rest, never logged, never in a response.** *Check: row L-06.*
- **Yahoo requests read-only scope only.** *Check: row L-05.*
- **Sleeper's endpoint list equals its fixture set.** *Check: row L-07.*
- **The CSV template and parser share one schema.** *Check: row L-10 round-trips the
  generated template through the parser.*
- **Unmappable settings fail by name.** *Check: row L-01.*
- **Import is non-destructive until confirmed.** *Check: row L-02.*

# Change procedure

Follow [add_new_data_source](../tasks/add_new_data_source.md). A new provider adds
its canonical name, its mapping rules in the contract, its fixtures, and its rows —
and must pass row L-04 with itself disabled.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md), rows
L-01 through L-10.
