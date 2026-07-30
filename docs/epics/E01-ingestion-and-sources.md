# E01 — Ingestion pipeline and data sources

**Requirements:** R2, R3, R4 · **Depends on:** nothing (steps 1–4 are done)
**Covers:** `AGENT_INSTRUCTIONS.md` steps 5–9

Player identity, provenance, the HTTP pipeline, the balldontlie provider, the
Basketball-Reference season scraper, and ADP. This is the epic that turns a tested
calculator into an application with its own data.

## Pre-flight

`dotnet --list-sdks` shows 10.0.x · `docker info` succeeds ·
`python3 context/validate_okf.py` exits 0 · `scripts/gate.sh` green.

You need a **balldontlie API key** (free tier) in user-secrets as
`BallDontLie:ApiKey` for the live import path. The tests do not need it.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E01 in this repository: the ingestion pipeline and data sources.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R2, R3,
   R4), ARCHITECTURE.md, AGENT_INSTRUCTIONS.md steps 5-9, and
   context/AGENT_CONTEXT_INDEX.md. Then read, in full:
     context/codebase_okf/contracts/player_identity_contract.md
     context/codebase_okf/contracts/provenance_contract.md
     context/codebase_okf/contracts/provider_contracts.md
     context/codebase_okf/components/ingestion_pipeline.md
     context/codebase_okf/components/scrapers.md
     context/codebase_okf/safety/scraping_policy.md
     context/codebase_okf/tests/test_matrix_ingestion_scrapers.md
   The specs are canonical. Where a decision is recorded, implement it exactly.

2. Scope, in order:
   a. PlayerIdentityResolver — the normalization algorithm and the matching
      ladder, exactly as written. Ambiguity produces a PendingIdentityMatch and
      never a guess.
   b. DataProvenance stamped on every imported row; non-nullable columns.
   c. The ingestion pipeline: IHttpClientFactory named clients per host, the
      resilience handler, a process-wide per-host rate limiter, response cache
      with per-source freshness windows, and DataImportRun recording.
   d. BallDontLieProvider for teams, players, and games.
   e. BasketballReferenceStatsScraper for the three permitted season pages, with
      an allowlist-enforcing URL builder and committed HTML fixtures.
   f. ADP: the FantasyPros scraper plus CSV import plus manual entry — three
      implementations of one contract.

3. Required test rows before the corresponding commit: N-01 to N-05, I-01 to
   I-13, S-10 to S-14, W-01 to W-03. Capture fixtures by hand, trimmed, and
   commit them. No test may make a real network request.

4. Commit after each of a-f with a conventional-commit message. Never leave the
   tree broken at a commit boundary. Update OKF concept status in the same commit
   as the code and tests that justify it. Commit locally; do not push.

5. Budget ladder if you run short: (a) identity + provenance; (b) the pipeline;
   (c) balldontlie; (d) the Basketball-Reference scraper; (e) ADP. Stop at a
   green commit boundary rather than starting f.

6. Non-negotiable: only the pins in stack_config.toml, nothing from its forbidden
   list; scraping stays inside the allowlist in safety/scraping_policy.md at or
   below 6 requests per minute; no test touches the network or a developer
   database; no placeholder files. Never weaken a safety concept or a test to
   make the gate pass.

Wrap-up at ~10% budget remaining: make what exists green, sync every concept
status to reality, run python3 context/validate_okf.py, append current state and
the single next step to context/codebase_okf/assumptions.md, and commit.

Final report: implemented / partial / missing, every command run with its actual
result and the SDK version used, any invariant you could not satisfy and why, and
the one next step. Under-claim rather than over-claim.
```

---

## Exit gate

`scripts/gate.sh` green, with rows N-01…N-05, I-01…I-13, S-10…S-14, W-01…W-03
passing, and a live import of players, teams, games, and one season of stats
succeeding against a real key.
