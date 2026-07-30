# E09 — Provider integrations

**Requirements:** R16 · **Depends on:** E04

Yahoo OAuth, Sleeper, and CSV import behind one snapshot interface. Removes the
biggest real-world friction — manual roster entry — without letting any provider
become load-bearing.

## Pre-flight

E04 landed green · `scripts/gate.sh` green.

You need a **Yahoo developer app** (client id and secret, read-only) for the live
path. Tests do not need it.

---

```text
You are explicitly authorized to implement: create and edit files, run commands,
and commit. Any planning-first restriction in CLAUDE.md is lifted for this
session. Proceed without waiting for further authorization.

Build epic E09 in this repository: the league import providers.

1. Read in order: AGENTS.md, stack_config.toml, PROJECT_REQUIREMENTS.md (R16),
   ARCHITECTURE.md post-MVP section, and context/AGENT_CONTEXT_INDEX.md. Then
   read, in full:
     context/codebase_okf/contracts/league_import_contract.md
     context/codebase_okf/components/league_import_adapters.md
     context/codebase_okf/contracts/provider_contracts.md
     context/codebase_okf/safety/secrets_policy.md
     context/codebase_okf/safety/scraping_policy.md
     context/codebase_okf/tasks/add_new_data_source.md
     context/codebase_okf/tests/test_matrix_ingestion_scrapers.md (rows L-01 to L-10)
   Add CsvHelper 33.1.0 from stack_config.toml [dependencies.epic].

2. Scope, in order:
   a. FantasyLeagueSnapshot and the mapping layer. Every provider produces the
      same snapshot; nothing downstream may branch on which provider it came from.
   b. CSV import FIRST, not last. It is the rung that makes every provider above
      it optional, and the template and the parser must be generated from ONE
      schema definition so they cannot disagree.
   c. The diff-and-confirm flow: fetch, map, diff against the existing league with
      MANUAL OVERRIDES HIGHLIGHTED SEPARATELY, require confirmation, apply as a
      new revision. Import never writes over a league in place — manual entry is
      authoritative, so reverting a user's correction needs their consent.
   d. Yahoo: authorization-code flow with refresh, READ-ONLY scope only (fspt-r).
      Never request a write scope — automated transactions are permanently out of
      scope. Tokens encrypted with Data Protection, stored as owned data, redacted
      from logs. Refresh failure marks the connection stale and prompts a
      reconnect; it never retries into a lockout.
   e. Sleeper: read-only, no auth. Its documentation describes NFL-only behavior in
      several places, so an endpoint is UNSUPPORTED until validated against real
      NBA data with that response committed as a fixture. Derive the adapter's
      supported-endpoint list FROM the fixture set, so an unvalidated endpoint is
      structurally uncallable rather than merely undocumented.
   f. ESPN: CSV and manual only. No authenticated scrape, ever. Do not add an ESPN
      adapter of any other kind.

3. THE FAIL-LOUD RULE, which is the most important behaviour in this epic: an
   import that cannot be represented EXACTLY fails, naming the setting. It never
   approximates. A scoring stat with no StatKey, an unmodelled league type, a
   roster slot outside the catalog, a category set that is not a subset of ours —
   all of these fail the import.

   The reasoning: every downstream number claims to be computed under the user's
   exact league rules. An import that quietly rounds a rule off invalidates every
   recommendation after it, silently and permanently. A failed import with a named
   cause costs five minutes of manual entry, which always works.

4. Required test rows: L-01 to L-10. Row L-04 runs the FULL MVP flow with every
   adapter unregistered — that row is the design doc's central rule, and if it
   fails the epic has made a provider load-bearing.

5. Commit after each of a-f. Never leave the tree broken at a commit boundary.
   Update OKF concept status in the same commit as its evidence. Commit locally.

6. Budget ladder: (a) snapshot + CSV; (b) diff-and-confirm; (c) Yahoo; (d) Sleeper.
   CSV plus diff-and-confirm is a complete, useful epic on its own.

7. Non-negotiable: read-only scopes only; no token in any log or response; no
   authenticated or private-page scrape; no branch on provider name outside the
   adapter assemblies; every adapter removable with the app still fully usable; no
   placeholder files.

Wrap-up at ~10% budget: green, statuses synced, validator clean, assumptions.md
updated with which Sleeper endpoints were validated, commit.

Final report: implemented / partial / missing, which Sleeper endpoints have
committed NBA fixtures, the L-04 result, and the one next step. Under-claim rather
than over-claim.
```

---

## Exit gate

Rows L-01…L-10 green, a real Yahoo league imported through the diff-and-confirm flow,
an unmappable setting failing by name, and row L-04 passing with every adapter
disabled.
