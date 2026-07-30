---
type: contract
title: Provenance Contract
description: The metadata block required on every imported row, its field semantics, and the hash and parser-version rules.
tags: [contract, provenance, ingestion]
source_paths: [src/FantasyBasketball.Domain/Provenance]
test_paths: [tests/FantasyBasketball.IntegrationTests/Ingestion]
depends_on: [provider_contracts.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - No imported row can be persisted without provenance.
  - "Why does the app believe X?" is answerable from the database alone.
---

# Responsibility

Owns the traceability metadata carried by every value that came from outside the
application. Its purpose is answering questions like *why does the app think
this player changed teams* and *which source supplied this stat line* without
guessing.

# The block

Shape is in [`ARCHITECTURE.md`](../../../ARCHITECTURE.md); field semantics are here.

| Field | Meaning |
|---|---|
| `Source` | A canonical source name from [provider_contracts](provider_contracts.md) |
| `ExternalId` | The source's own id for the record, when it has one |
| `FetchedAt` | When *we* retrieved it (UTC) |
| `SourceTimestamp` | When the *source* says the data was current, when published |
| `ParserVersion` | Version of the adapter that produced this row |
| `Confidence` | `0..1`; how much the pipeline trusts this row |
| `RawRecordHash` | SHA-256 of the raw source fragment this row was parsed from |

# Confidence defaults

Starting values by source kind. Anything else is set explicitly and explained.

| Source kind | Default |
|---|---|
| Official/documented API (`balldontlie`) | `0.95` |
| HTML scraper (`basketball-reference`, `fantasypros`) | `0.85` |
| CSV import | `0.80` |
| Manual entry | `1.00` — the user is authoritative about their own league |

These feed the recommendation confidence model in
[recommendation_evidence_contract](recommendation_evidence_contract.md); they
are not displayed raw.

# ParserVersion

Format: `{source}-v{n}` — `basketball-reference-v1`.

**Bumped whenever a parser's output could change for identical input**: a column
map change, a new derived field, a fixed rounding bug. Not bumped for internal
refactors that provably cannot change output. The bump is what lets a
re-import be distinguished from a re-parse when auditing a suspect value.

# Invariants

- **Every imported row has provenance.** `DataProvenance` is non-nullable on
  every entity that can originate outside the app. There is no code path that
  writes an imported row without it. *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row I-07, plus a persistence test asserting the column is `NOT NULL`.*
- **`FetchedAt` is UTC and comes from an injected `TimeProvider`**, never
  `DateTime.Now`. *Check: the `DateTime\.Now` forbidden-pattern scan.*
- **`RawRecordHash` is computed from the raw fragment before parsing**, so a
  re-parse of unchanged input is detectable. *Check: row I-10 re-imports an
  unchanged fixture and asserts the hash is stable.*
- **Manual entry is provenance too.** A user-entered roster or context event
  records `Source = manual`; it is not exempt. *Check: row I-11.*
- **Provenance is never overwritten in place.** A newer import writes a new row
  or a new version; it does not mutate the old one's provenance. See
  [data_integrity_policy](../safety/data_integrity_policy.md).

# Change procedure

Adding a field: this file, `DataProvenance`, the EF configuration, a migration,
and every adapter that constructs it — one commit.

# Verification

`test_matrix_ingestion_scrapers.md` rows I-07, I-10, I-11, plus the schema
assertion that provenance columns are `NOT NULL`.

# Implementation evidence

`DataSourceName` is the single code catalog for the five canonical names and
`DataProvenance` now rejects unknown sources, malformed parser versions,
non-UTC timestamps, out-of-range confidence, and non-SHA-256 hashes.
`ExternalPlayer` and `ExternalTeam` require provenance at construction.
Imported team source rows and NBA games persist every provenance field as
non-null columns, with integration round trips; unchanged provider fragments
produce stable SHA-256 hashes. Basketball-Reference season rows use the
canonical source, injected UTC time, HTML-scraper confidence,
`basketball-reference-v1`, the external player slug, and a SHA-256 hash of the
three raw row fragments. The concept remains `partial` until every scraper,
CSV, and manual adapter persists its output and I-11 passes.
