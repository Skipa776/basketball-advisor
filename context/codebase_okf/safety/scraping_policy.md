---
type: safety_policy
title: Scraping Policy
description: The host allowlist with exact permitted paths, disallowed patterns, crawl rates, and the re-verification procedure.
tags: [safety, scraping, compliance]
source_paths: [src/FantasyBasketball.Infrastructure/Scrapers, src/FantasyBasketball.Infrastructure/Http]
test_paths: [tests/FantasyBasketball.IntegrationTests/Scrapers]
depends_on: [../contracts/provider_contracts.md]
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - Every scraper URL is produced by a builder that enforces this allowlist.
  - A disallowed path cannot be requested even by a caller that tries.
  - Rate limiting is enforced per host, not per client instance.
---

# Responsibility

Owns what this application is permitted to fetch and how fast. Scraping is a
controlled infrastructure capability here, not ad-hoc HTML parsing sprinkled
through the codebase. **You may not weaken this file to make a feature work.**

# Host allowlist

Nothing outside this table may be requested by a scraper. Requesting an
unlisted host is a bug that fails a test, not a runtime decision.

## basketball-reference.com — *robots verified 2026-07-29*

`Crawl-delay: 3`. Sports-Reference blocks clients exceeding **20 requests per
minute** and jails the session for up to a day.

| Permitted | Purpose |
|---|---|
| `/leagues/NBA_{year}_per_game.html` | Season per-game stats |
| `/leagues/NBA_{year}_totals.html` | Season totals — the per-minute rate input |
| `/leagues/NBA_{year}_advanced.html` | Usage rate |
| `/players/{letter}/{slug}.html` | Player metadata, only when identity resolution needs it |

**Disallowed by robots — never request:**
`*/gamelog/`, `*/splits/`, `*/on-off/`, `*/lineups/`, `*/shooting/`,
`/play-index/*.cgi?*`, `/req/`, `/short/`, `/nocdn/`, `/my/`, `/fc/`, `/dump/`,
`/basketball/`, `/blazers/`.

> **`*/gamelog/` is disallowed, and it is the obvious source for per-game
> stat lines.** The MVP therefore uses season-level pages only — three requests
> per season import. Rolling windows (design-doc phase 6) must use `/boxscores/`
> instead, which is permitted but is roughly 1230 pages per season and so belongs
> to a rate-limited background worker, never an interactive import. See
> [post_mvp_roadmap](../tasks/post_mvp_roadmap.md).

## fantasypros.com — *robots verified 2026-07-29*

`Crawl-delay: 5`.

| Permitted | Purpose |
|---|---|
| `/nba/adp/overall.php` | ADP for draft market value |

**Disallowed by robots — never request:** `/api/`, `/json/`, `/xml/`,
`/nba/ranker/`, `/ajax/`. The JSON endpoints are explicitly off limits, so the
HTML page is the only legitimate target.

## api.balldontlie.io

An API, not a scrape: authenticated with our own key, used within its documented
free-tier limit of **5 requests per minute**. Not subject to the crawl rules
above, but subject to the same rate limiter.

# Rules

- **Rate limits are per host and shared process-wide**, from
  [`stack_config.toml`](../../../stack_config.toml) `[scraping]`: 6 requests per
  minute global ceiling, 10-second default delay. That is deliberately far below
  every host's ban threshold — the cost of being slow is nothing, the cost of a
  ban is losing the data source during draft season.
- **Identify honestly.** The configured User-Agent names the application. Never
  impersonate a browser to evade detection.
- **Cache first.** A cached response within its freshness window is served
  without a request. Season stat pages change at most daily.
- **Never bypass authentication or anti-bot controls.** No cookie replay, no
  captcha solving, no header spoofing beyond an honest User-Agent.
- **Never automate access to private league pages** on any fantasy platform.
  ESPN league data enters through manual entry or CSV, never through an
  authenticated scrape.
- **One parser per site, isolated.** A site redesign breaks one parser and one
  test, never the domain.
- **Back off on 429 or 5xx** with exponential backoff, and stop after the
  configured retry budget rather than hammering.

# Invariants

- **Every scraper URL comes from a host-specific URL builder** that validates
  against this allowlist; scrapers never concatenate URLs inline. *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row S-10 asks each builder for a `/gamelog/` path and asserts it throws.*
- **A disallowed pattern is rejected even when explicitly requested.** The
  builder is a gate, not a convenience. *Check: row S-10.*
- **The rate limiter is asserted, not assumed**: a test issues 20 rapid requests
  through the pipeline and asserts elapsed time consistent with the configured
  ceiling. *Check: row S-11.*
- **No test makes a real network request.** All parser tests run from saved
  fixtures. *Check: row S-12 installs a handler that throws on any real send.*
- **Robots verification has an expiry.** The dates in this file are part of the
  contract; see below.

# Re-verification procedure

Robots directives change. Before any scraper runs against a live host after a
long gap, and at minimum **every 90 days**:

1. Fetch `https://{host}/robots.txt` by hand.
2. Diff the permitted and disallowed paths against the tables above.
3. Update this file's tables and its verification date in the same commit.
4. If a permitted path became disallowed, delete the code that used it before
   the next run — do not leave it behind a flag.

Terms of Use are a human responsibility and are not machine-checked; the
operator reads the current terms before running against a live host. Recorded as
knowingly-unenforced in [assumptions](../assumptions.md).

# Verification

`test_matrix_ingestion_scrapers.md`, rows S-10 through S-12.

# Implementation evidence

The process-wide per-host limiter is implemented with
`System.Threading.RateLimiting`, registered once in the API composition root,
and tested with 20 requests split across two handlers. Production registration
uses the contract's 10-second minimum interval and honest User-Agent. URL
enforcement is now implemented for Basketball-Reference: `BbrefUrlBuilder`
accepts only the three season pages and player metadata shape on the canonical
HTTPS host, rejects query strings, fragments, foreign hosts, and all other
paths, and is covered by the S-10 gamelog rejection test.
`FantasyProsUrlBuilder` accepts only the canonical HTTPS host and exact
`/nba/adp/overall.php` path, rejecting API, gamelog, query, fragment, alternate
port, user-info, and foreign-host forms. Both scrapers use their builders and
the shared process-wide limiter, so the policy's done criteria are implemented;
no permitted or disallowed path was changed.
