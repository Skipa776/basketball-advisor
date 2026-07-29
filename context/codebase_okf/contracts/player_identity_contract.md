---
type: contract
title: Player Identity Contract
description: Canonical player identity, the exact name-normalization algorithm, the matching ladder, and the ambiguity rule.
tags: [contract, identity, ingestion]
source_paths: [src/FantasyBasketball.Domain/Players, src/FantasyBasketball.Application/Ingestion/PlayerIdentityResolver.cs]
test_paths: [tests/FantasyBasketball.Application.Tests/Ingestion]
depends_on: [provider_contracts.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
done_criteria:
  - A provider id never becomes a primary key.
  - Ambiguous names produce a pending match, never a wrong link.
  - The normalization examples below round-trip exactly.
---

# Responsibility

Owns how a player from any external source becomes *our* player. Different
platforms use different ids and spell names differently; a wrong link silently
merges two players' statistics, which is the worst failure mode in the system
because nothing downstream can detect it.

# Canonical identity

`PlayerId` (a `Guid` wrapper) is the application's only player identity.
Provider ids are stored as `ExternalPlayerIdentity(PlayerId, Provider,
ExternalId, LinkedAt, ConfirmedByHuman)` rows — many per player, one per
provider. Provider names come from [provider_contracts](provider_contracts.md).

**A provider id is never a primary key, a foreign key, or a dictionary key
anywhere in the domain.** The design doc calls this out and it is the reason the
app survives a provider disappearing.

# Normalization algorithm

Applied in this exact order to produce `Player.NormalizedName`:

1. Unicode normalize to NFD, then strip all combining marks — `Jokić` → `Jokic`.
2. Lowercase, invariant culture.
3. Remove `.`, `'`, `’`, `-`, and `,` entirely (not replaced with a space) —
   `De'Aaron` → `deaaron`, `Karl-Anthony` → `karlanthony`, `P.J.` → `pj`.
4. Drop trailing generational suffix tokens: `jr`, `sr`, `ii`, `iii`, `iv`, `v`.
   Only as a **final** token, so `Vince Carter V` loses the suffix but a player
   legitimately named `V` as a first token does not.
5. Collapse runs of whitespace to one space; trim.

Worked examples, which are also the test cases:

| Input | Normalized |
|---|---|
| `Nikola Jokić` | `nikola jokic` |
| `De'Aaron Fox` | `deaaron fox` |
| `Karl-Anthony Towns` | `karlanthony towns` |
| `P.J. Tucker` | `pj tucker` |
| `Jaren Jackson Jr.` | `jaren jackson` |
| `  Luka   Dončić ` | `luka doncic` |

# The matching ladder

Tried in order; the first that resolves wins.

| Tier | Condition | Result |
|---|---|---|
| 1 | An `ExternalPlayerIdentity` already exists for `(provider, externalId)` | Resolved |
| 2 | `NormalizedName` matches exactly one player **and** NBA team matches | Auto-link, `ConfirmedByHuman = false` |
| 3 | `NormalizedName` matches exactly one player in the whole pool | Auto-link, `ConfirmedByHuman = false` |
| 4 | `NormalizedName` + birth date both match exactly one player | Auto-link, `ConfirmedByHuman = false` |
| 5 | Zero matches | Create a new player, link, `ConfirmedByHuman = false` |
| 6 | More than one match survives tiers 2–4 | **`PendingIdentityMatch`** — no link is written |

# Invariants

- **Ambiguity never auto-resolves.** Two candidates means a pending match for a
  human, never a best guess. *Check: `test_matrix_ingestion_scrapers.md` row
  N-03 feeds two same-normalized-name players and asserts zero links written.*
- **No fuzzy matching in the MVP.** No edit distance, no phonetic matching, no
  nickname table. They trade a visible failure (pending match) for an invisible
  one (wrong merge). *Check: prose — no fuzzy matcher exists to test.*
- **Normalization is pure and deterministic** — no culture sensitivity, no
  clock, no I/O. *Check: row N-01 runs the table above.*
- **A pending match blocks the affected rows, not the whole import.** The import
  run completes, records the pending count, and stays queryable. *Check: row N-04.*
- **Linking is append-only.** Re-running an import never rewrites an existing
  link; a conflicting id creates a pending match instead. *Check: row N-05.*

# Change procedure

Changing normalization invalidates every stored `NormalizedName`. That is a
migration plus a re-normalization pass, in the same commit as this file, plus
regenerating the examples above.

# Verification

`test_matrix_ingestion_scrapers.md`, rows N-01 through N-05.
