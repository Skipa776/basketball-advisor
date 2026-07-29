---
type: task
title: Add a New Data Source
description: The playbook for adding an API provider or a scraper, including fixture capture and the compliance check.
tags: [task, providers, scrapers]
source_paths: [src/FantasyBasketball.Infrastructure/Providers, src/FantasyBasketball.Infrastructure/Scrapers]
test_paths: [tests/FantasyBasketball.IntegrationTests]
depends_on: [../contracts/provider_contracts.md, ../safety/scraping_policy.md, ../components/scrapers.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
---

# Responsibility

Owns the repeatable steps for connecting a new source without weakening the
boundaries that keep the domain source-independent.

# Before writing code

1. **Which rung?** Official API > public structured endpoint > permitted page
   scrape > CSV > manual. Take the highest rung that actually works. A scraper
   where an API exists is maintenance you chose to buy.
2. **Which interface?** It must implement an existing one from
   [provider_contracts](../contracts/provider_contracts.md). If none fits, the
   contract changes first, in its own commit, with a reason.
3. **Compliance check, for any scrape:**
   - fetch and read the host's current `robots.txt`;
   - confirm the exact paths you need are not disallowed;
   - read the host's terms of use — a human judgment, not a machine check;
   - add the host, its permitted paths, its disallowed patterns, its crawl
     delay, and today's date to
     [scraping_policy](../safety/scraping_policy.md).

   **No allowlist entry, no code.** This step is not optional and not deferrable.

# Building it

4. **URL builder first.** It validates against the allowlist and throws on a
   disallowed path. Test that it throws before writing the parser.
5. **Capture a fixture.** Fetch the page once by hand, trim it to the relevant
   table plus surrounding structure, save as
   `tests/.../Fixtures/Html/{source}-{page}-{yyyy-MM-dd}.html`, commit it.
6. **Parser as a pure function** of the fixture string. Write the test from the
   fixture's real values first; make it pass second.
7. **Map columns through
   [stat_vocabulary](../contracts/stat_vocabulary.md)** — add the source's map
   there, not as literals in the parser.
8. **Compose the adapter**: fetch through the ingestion pipeline (never a raw
   `HttpClient`), parse, stamp provenance with `ParserVersion` at `v1`.
9. **Register it** with its own named client, its own rate limiter entry, and
   its own cache freshness window.
10. **Contract test**: recorded payload in, canonical models out.

# Landing it

11. Add the canonical source name to
    [provider_contracts](../contracts/provider_contracts.md), and its role to
    `[data_sources]` in [`stack_config.toml`](../../../stack_config.toml) if it
    takes one.
12. Add its rows to
    [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md).
13. Confirm the category still works with the new source **disabled** — a new
    source must not become a dependency.
14. Run the full gate. One commit.

# When a source breaks

A parser test failing after a site redesign is the system working. Capture a
fresh fixture, fix the parser, bump `ParserVersion`, update the column map if
headers changed. **Never loosen the test to make it pass** — that trades a loud
failure for silent data corruption.

If a source disappears permanently: delete its adapter, its allowlist entry, and
its fixtures; confirm the category still functions on a lower rung; record it in
[assumptions](../assumptions.md).

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md) —
the new source needs its own contract-test row and its own allowlist-enforcement
row.
