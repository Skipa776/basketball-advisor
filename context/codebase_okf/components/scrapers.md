---
type: component
title: Scrapers
description: Per-site parser isolation, allowlist-enforcing URL builders, fixture-based testing, and parser versioning.
tags: [component, scrapers, parsing]
source_paths: [src/FantasyBasketball.Infrastructure/Scrapers]
test_paths: [tests/FantasyBasketball.IntegrationTests/Scrapers, tests/FantasyBasketball.IntegrationTests/Fixtures/Html]
depends_on: [../safety/scraping_policy.md, ../contracts/stat_vocabulary.md]
status: partial
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - Every parser is tested from saved HTML with zero network access.
  - A site redesign fails a parser test rather than writing wrong data.
---

# Responsibility

Owns HTML acquisition and parsing for Basketball-Reference and FantasyPros. What
may be fetched is owned by [scraping_policy](../safety/scraping_policy.md);
column mappings are owned by
[stat_vocabulary](../contracts/stat_vocabulary.md). This file owns structure.

# Design

Each site gets three isolated pieces:

- **URL builder** — the only place a URL is constructed, validating against the
  allowlist. It is a gate: asked for a disallowed path, it throws.
- **Parser** — pure `string html → IReadOnlyList<T>`. No HTTP, no clock, no
  database, so every parser test is a fixture and an assertion.
- **Scraper** — composes fetch (through the ingestion pipeline) with parse, and
  stamps provenance.

Parsing uses AngleSharp with CSS selectors. Basketball-Reference's season tables
are wrapped in HTML comments for some views; the parser handles that explicitly
rather than by regex-stripping the whole document.

# Fixtures

Saved pages live in `tests/.../Fixtures/Html/`, named
`{source}-{page}-{yyyy-MM-dd}.html`, committed. Capturing a new one is step 1 of
[add_new_data_source](../tasks/add_new_data_source.md). Fixtures are trimmed to
the relevant table plus surrounding structure — a whole page is megabytes and
makes diffs unreadable.

# Invariants

- **Parsers are pure functions of HTML.** *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row S-13 parses a fixture twice and asserts identical output.*
- **Every URL comes from the builder; the builder enforces the allowlist.**
  *Check: row S-10.*
- **A malformed or restructured table fails loudly.** Missing expected columns
  throws a parse error naming the column; it never yields a row of zeros.
  *Check: row S-14 feeds a fixture with a renamed column.*
- **Imported rows satisfy the stat vocabulary's identities** — `REB = OREB +
  DREB`, ratios in `0..1`, percentage-point sources divided at the boundary.
  *Check: rows I-05 and the identity assertions.*
- **`ParserVersion` is bumped whenever output could change for identical
  input.** *Check: review, per
  [provenance_contract](../contracts/provenance_contract.md).*
- **Zero network access in tests.** *Check: row S-12.*
- **One parser per site.** A FantasyPros change can never break the
  Basketball-Reference path. *Check: structural; no shared parser class exists.*

# Change procedure

A site redesign: capture a fresh fixture, fix the parser, bump `ParserVersion`,
update the column map in the stat vocabulary if headers changed — one commit.
**Never loosen a parser test to make it pass**; that converts a loud failure
into silent data corruption, which is the exact failure mode fixtures exist to
prevent.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md),
rows S-10 through S-14.

# Implementation evidence

`BasketballReferenceStatsScraper` fetches only the three season URLs produced by
`BbrefUrlBuilder`, parses saved per-game, totals, and advanced fixtures through
the pure `SeasonTableParser`, resolves canonical player identity, and stamps
`basketball-reference-v1` provenance. Tests cover comment-wrapped tables,
repeatable parsing, `USG%` conversion, rebound identities, a renamed required
column, and rejection of both a disallowed gamelog path and a foreign host.
FantasyPros remains for the ADP slice, so this component is `partial`.
