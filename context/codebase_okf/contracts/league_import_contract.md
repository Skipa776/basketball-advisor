---
type: contract
title: League Import Contract
description: The canonical league snapshot, per-provider mapping rules, the fail-loud-on-unmappable rule, and the non-destructive import flow.
tags: [contract, providers, import]
source_paths: [src/FantasyBasketball.Infrastructure/Providers, src/FantasyBasketball.Infrastructure/Import]
test_paths: [tests/FantasyBasketball.IntegrationTests/Providers]
depends_on: [provider_contracts.md, scoring_rules_catalog.md, player_identity_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - An unmappable provider setting fails the import by name; it is never approximated.
  - Import never silently overwrites a user's manual configuration.
  - Every provider can be disabled and the app stays fully usable.
---

# Responsibility

Owns turning someone else's league into ours. The architectural rule from the
design doc applies at full strength here: **the domain must never learn which
provider a league came from.** Every provider produces the same snapshot.

# The canonical snapshot

```text
FantasyLeagueSnapshot
  Provider            canonical name from provider_contracts.md
  ExternalLeagueId
  Name, TeamCount, LeagueType, LineupCadence
  ScoringRules[]      or Categories[]
  RosterSlots[]
  TradeDeadline, PlayoffStartWeek, WeeklyAcquisitionLimit
  Teams[]             ExternalTeamId, Name, IsUsersTeam
  RosterEntries[]     ExternalTeamId, ExternalPlayerId, SlotKind, AcquiredAt
  FetchedAt, Provenance
```

Players resolve through
[player_identity_contract](player_identity_contract.md) — an unresolved player
becomes a pending match, and the import completes with that count reported.

# The fail-loud rule

**An import that cannot be represented exactly fails, naming the setting.** It
never approximates.

Examples that must fail rather than degrade: a scoring stat with no `StatKey`; a
league type we do not model; a roster slot kind outside the catalog; a
category league whose category set is not a subset of ours.

The reasoning is the whole product: every downstream number claims to be computed
under *your league's exact rules*. An import that quietly rounds a rule off
invalidates every recommendation that follows, silently and permanently. A failed
import with a named cause costs the user five minutes of manual entry — which
always works ([provider_contracts](provider_contracts.md)).

# Non-destructive import

Import never writes over an existing league in place:

1. Fetch and map to a snapshot.
2. Diff against the existing league, if any.
3. Present the diff — **manual overrides highlighted separately** — and require
   confirmation.
4. Apply as a new revision, preserving the prior configuration.

Manual entry is authoritative
([data_integrity_policy](../safety/data_integrity_policy.md) rule 6), so an
import that would revert a user's correction must say so and get consent.

# Providers

## Yahoo — OAuth 2.0, read-only

- Authorization-code flow with refresh; scope is **read-only** (`fspt-r`).
  Write scopes are never requested — the app does not submit transactions
  ([post_mvp_roadmap](../tasks/post_mvp_roadmap.md) keeps automated transactions
  permanently out of scope).
- Tokens are encrypted at rest via ASP.NET Data Protection and never logged
  ([secrets_policy](../safety/secrets_policy.md)).
- Refresh failure marks the connection stale and surfaces a reconnect prompt; it
  never silently retries into a lockout.
- Settings, teams, and rosters map to the snapshot above.

## Sleeper — read-only HTTP, no auth

Sleeper's documentation describes NFL-only behavior in several places, so:

**An endpoint is unsupported until it has been validated against real NBA data
and that response is committed as a fixture.** No endpoint may be used on the
strength of the documentation alone. Each validated endpoint gets a fixture and a
contract-test row; an unvalidated one is simply not implemented, and the adapter
reports partial support rather than guessing.

## ESPN — CSV and manual only

No authenticated scrape, ever
([scraping_policy](../safety/scraping_policy.md)). ESPN leagues come in through
CSV export or manual entry. This is the design doc's explicit constraint and it
does not relax.

## CSV — the guaranteed rung

A documented column format with a template a user can download, fill, and
re-upload. This is the bottom of the source ladder and must always work, because
it is what makes every provider above it optional.

# Invariants

- **An unmappable setting fails the import by name.** *Check:
  [test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md)
  row L-01 imports a league with an unknown scoring stat and asserts a
  `validation_failed` naming that stat, with no league created.*
- **Import is non-destructive** — an existing league is unchanged until confirmed.
  *Check: row L-02.*
- **Manual overrides are flagged in the diff.** *Check: row L-03.*
- **Every provider disabled still leaves the app fully usable.** *Check: row L-04
  runs the full MVP flow with every adapter unregistered.*
- **Yahoo requests read-only scope.** *Check: row L-05 asserts the authorization
  URL contains no write scope.*
- **Tokens never appear in logs or responses.** *Check: row L-06.*
- **Sleeper endpoints without a committed NBA fixture are not called.** *Check:
  row L-07 asserts the adapter's endpoint list equals the fixture set.*
- **Snapshots carry provenance** and unresolved players become pending matches
  without failing the run. *Check: row L-08.*

# Change procedure

Adding a provider: follow
[add_new_data_source](../tasks/add_new_data_source.md), add the canonical name,
add mapping rules here, and add its contract-test rows.

# Verification

[test_matrix_ingestion_scrapers](../tests/test_matrix_ingestion_scrapers.md),
rows L-01 through L-08.
