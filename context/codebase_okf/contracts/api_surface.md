---
type: contract
title: API Surface Contract
description: The response envelope, error codes, validation semantics, and per-endpoint behavior for the HTTP API.
tags: [contract, api, http]
source_paths: [src/FantasyBasketball.Api/Endpoints]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [recommendation_evidence_contract.md, scoring_rules_catalog.md]
status: implemented
last_updated: 2026-09-23
owners: [engineering]
risk_level: low
done_criteria:
  - Every endpoint returns the envelope, including on error.
  - No unhandled exception ever reaches the client as a stack trace.
---

# Responsibility

Owns the HTTP contract. Routes are listed in
[`ARCHITECTURE.md`](../../../ARCHITECTURE.md); this file owns the envelope,
error semantics, and behaviors that are not obvious from a route.

# Envelope

Every response, success or failure:

```jsonc
{
  "success": true,
  "data":    { },        // null on failure
  "error":   null,       // { "code": "...", "message": "...", "fields": {} } on failure
  "meta":    null        // { "total": 0, "page": 1, "limit": 50 } on paged responses
}
```

Produced by one result helper and the exception middleware. Endpoints do not
hand-build it.

# Error codes

| Code | HTTP | When |
|---|---|---|
| `validation_failed` | 400 | Input failed validation; `fields` names each offender |
| `not_found` | 404 | Addressed resource does not exist |
| `conflict` | 409 | Violates a uniqueness or lifecycle rule (duplicate pick, re-verifying) |
| `source_unavailable` | 503 | A provider or scraper failed; the app is degraded, not broken |
| `rate_limited` | 429 | The caller exceeded a limit |
| `internal_error` | 500 | Anything else; message is generic, details are logged |

`internal_error` **never** returns exception text, stack traces, connection
strings, or SQL. *Check:
[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-10
forces a throw and asserts the body contains none of these.*

# Endpoint semantics

- **`POST /api/leagues`** — validates per
  [scoring_rules_catalog](scoring_rules_catalog.md); a league with no scoring
  rules or a duplicated stat is `validation_failed` with the offending field
  named. No provider default is ever substituted.
- **`GET /api/players/{id}/projection?leagueId=`** — returns the full
  decomposition (observed, baseline, adjusted, value) as four sibling objects,
  never a single collapsed number. This is requirement R8, and the response
  shape *is* the requirement.
- **`GET /api/public/daily?date=YYYY-MM-DD`** and
  **`GET /api/public/risers?through=YYYY-MM-DD&limit=1..50`** — anonymous,
  read-only, `Cache-Control: no-store`, rate-limited per client IP (60/min).
  Shared NBA reference data only (stored Basketball-Reference regular-season box
  scores), never user data. Date defaults to the latest stored game date.
  `daily` returns the featured players who played that date with ESPN-default
  fantasy points, categories won of the nine standard categories versus that
  day's pool, and their box line. `risers` excludes featured players and ranks
  the rest by points above their own baseline (heat policy), with streak,
  percent above baseline and a waiver status. Malformed dates or limits → 400.
- **`POST /api/imports/box-scores`** `{from, to}` (owner) — queues a
  regular-season Basketball-Reference box-score import over stored final
  balldontlie games in that US-Eastern date range; see `boxscore_importer`.
- **`POST /api/imports/*`** — returns the created `DataImportRun` id immediately
  with status `Running`; poll `GET /api/imports/runs`. A failed import returns
  `200` with a failed run, not `500` — a failed import is data, not a server
  error.
- **`POST /api/drafts/{id}/picks`** — idempotent on `(sessionId, pickNumber)`.
  Re-submitting the same pick returns the existing pick; submitting a *different*
  player for a taken pick number is `conflict`. **Double submission is the
  classic live-draft failure and is a required test**, not a theoretical one.
  *Check: row A-11.*
- **`DELETE /api/drafts/{id}/picks/{pickNumber}`** — only the most recent pick is
  undoable; undoing anything else is `conflict`. *Check: row A-12.*
- **`POST /api/context-events/{id}/verify`** — the **only** place a
  `VerificationState` becomes `Verified`. Verifying an already-verified or
  rejected event is `conflict`. *Check: row A-13.*
- **`GET /api/health/data-sources`** — per source: last success, last failure,
  staleness, current degradation. Always `200`, even when everything is broken;
  that *is* the information.
- **`GET /api/drafts?leagueId=&page=&limit=`** — requires ownership of the
  selected league, returns only that league's saved sessions and paging metadata.
  A missing league id is `400`; another user's league is `404`.
- **`PUT /api/leagues/{id}/settings`** — changes name, cadence, team count and
  roster slots. Name and cadence remain editable with existing drafts. Team
  count and roster-slot counts by kind return `409` after any draft session exists;
  reordering the same slots is not a structural change. Scoring
  remains a separate endpoint and requires explicit projection recalculation.
- **`GET /api/leagues/{id}/projected-players?page=&limit=`** — requires league
  ownership. Returns the league's current published values, the same set the
  draft board ranks, ordered by projected season value (ties by player id), each
  with rank, name, positions, latest ADP and the unverified-context flag. An
  empty list means no current values; it is `200`, not an error.
- **League names** — create and settings return `400` with a `name` field error
  for an empty name or one longer than 100 characters.

# Invariants

- **The envelope is universal**, including on 4xx and 5xx. *Check: row A-14
  sweeps every route and asserts envelope shape on a success and a failure.*
- **Validation happens at the boundary**, before any domain object is
  constructed. Domain types are valid by construction and do not re-validate.
- **Every endpoint takes the request's `CancellationToken`** and passes it down.
- **Paging is required wherever a list can grow unbounded** (`/api/players`,
  `/api/imports/runs`, `/api/context-events`): default limit 50, max 200.

# Change procedure

Adding an endpoint: route in `ARCHITECTURE.md`, semantics here if non-obvious,
the endpoint class, and an integration test — one commit.

# Verification

`test_matrix_api_persistence.md`, rows A-10 through A-14.

# Implementation evidence

All canonical MVP routes are grouped by resource and return the single
`ApiEnvelope` helper. Boundary DTO parsing reports named validation fields;
missing resources and lifecycle conflicts map to the catalogued status codes.
Exception handling is outermost and returns a generic `internal_error` without
exception text, SQL, stack traces, connection strings, or provider secrets.
The local-loopback HTTP integration test exercises every route family,
including decomposed projection siblings, queued `Running` imports, idempotent
picks, last-pick undo, context review conflicts, paging metadata, health during
degradation, framework 404s, and forced 500s.

## React integration additions — 2026-09-19

- `GET /api/account/session` is anonymous-readable and `Cache-Control: no-store`.
  Returns `authenticated`, nullable minimal `user` (id/displayName/isInstanceOwner),
  and `registrationOpen`. Registration remains enforced atomically at mutation.
- `GET /api/leagues` requires authentication, defaults to page 1 / limit 50
  (maximum 200), and returns only caller-owned leagues plus paging metadata.
- `GET /api/leagues/setup` requires authentication and exposes named stat/roster
  vocabulary and the explicit editable ESPN points starter. Creation still
  requires the submitted scoring rules; the server never substitutes defaults.
- `GET /api/drafts/{id}` requires explicit draft ownership and returns the
  persisted `leagueId` and `session`, including picks, to restore on refresh.

Existing numeric enum serialization stays compatible. The setup catalog supplies
name/value mappings so React does not duplicate the C# enums.

## React functional pages — 2026-09-22

- `/app/{page}` serves the same React client for data sources, saved drafts,
  league settings, context review, account data and named unsupported-capability
  surfaces. Unknown page names show a way back to the workspace.
- `GET /api/drafts?leagueId=&page=&limit=` checks league ownership before
  returning a bounded, league-scoped list. Its items contain the persisted
  session and picks so the interface can show progress and reopen by id.
- `PUT /api/leagues/{id}/settings` has cookie anti-forgery and explicit owner
  authorization. It validates the entire replacement before saving. Structural
  changes are refused after a draft exists; name and cadence remain editable.
- The React data-source page combines source freshness with saved runs to
  distinguish running, failed, no rows, stale and ready states.
- `BallDontLie:ApiKey` remains required at startup under the stable
  [secrets policy](../safety/secrets_policy.md).
- Context list responses retain numeric enum fields and add corresponding names
  for review controls; create/verify/reject use the existing human-review endpoints.
- Import actions are manual only. The browser journey does not click an import
  and no provider is contacted by tests.

## Projection publication — 2026-09-20

- `GET /api/leagues/{id}/projection-pools` requires explicit ownership. Returns
  up to 200 imported season/source groups, newest first, with source player counts.
- `POST /api/leagues/{id}/projections` requires ownership and cookie anti-forgery.
  Body: `{seasonEndYear, source}`. Only points leagues are currently supported.
  Invalid season/source is `400`; no matching imported pool or a category league
  is `409`. No request to an external provider occurs. Success returns
  `{seasonEndYear, source, playerCount, computedAt}` after one atomic publication.
  Manual corrections take precedence and may add players to the source pool.
- Recalculation appends immutable history. Current values match the current
  scoring profile and the latest complete publication; a scoring edit requires
  recalculation and never silently serves old scores as current. Previously
  persisted values without publication metadata require recalculation.
- Setup additionally supplies confidence and evidence-polarity enum mappings.
  React fetches recommendations on draft-state changes or explicit refresh;
  the existing recommendation endpoint persists evidence, so it is not polled.

## Recorded performance — 2026-09-21

- `GET /api/leagues/{id}/performance-pools`: owned, authenticated points-league
  access to recorded regular-season source/season groups. Empty is successful.
- `GET /api/leagues/{id}/performance`: requires explicit season, source,
  ISO through-date and best/hot/all view; standard pagination bounds apply.
  Returns ranked scored appearance windows plus date, policy, model and coverage
  metadata. GET reads current saved scoring and writes no ranking/projection.
- Success uses `Cache-Control: no-store`. Unknown/foreign league is 404,
  invalid selection is 400, categories are 409, anonymous is 401. HP-01–HP-05
  verify the pipeline including the now 15-route ownership sweep.

The complete shape and historical-data limitations are owned by
[player_performance_api_contract](player_performance_api_contract.md).

**Landing update — 2026-09-23.** `GET /api/public/daily` returns *every* featured
player's latest appearance on or before the resolved date; each line carries its
own `playedOn`, and `poolSize` counts the resolved date only. Setting
`Landing:AsOf` (a simulated "today", owner test setting) makes the default date
the last stored game day strictly before it, for `daily` and `risers` alike.

**Rosters — 2026-09-23.** `GET /api/leagues/{id}/teams` lists the league's teams
(name, `isUsersTeam`, players with names). `POST /api/leagues/{id}/teams/csv`
`{csv}` replaces them from `Team,Player[,Mine]` rows and returns counts plus
unmatched names; malformed input → 400 naming the line or rule. Both are owned
league routes (404 for another user's league).

**League waiver — 2026-09-23.** `GET /api/leagues/{id}/waiver?through=&limit=`
(owned, no-store) returns the risers shape scored under the league's own points
rules. It leaves out every rostered player (`excludes: "rostered"`,
`excludedPlayers`); without imported rosters it leaves out the featured stars and
says so (`excludes: "featured"`). Category leagues → 409 (points only for now,
owner decision).

**Matchup — 2026-09-23.** `GET /api/leagues/{id}/matchup?opponent=&date=`
(owned, no-store): Monday–Sunday week containing `date` (default: `Landing:AsOf`,
else today's US-Eastern date). Each side = points scored this week through
yesterday + games left from today × average over the last 10 appearances, all
under the league's points rules. 409 names the missing step (rosters, your team,
an opponent) or a category league.

**Weekly acquisitions — 2026-09-23.** Leagues carry `weeklyAcquisitionLimit`
(1–99, default 7, owner decision): optional on `POST /api/leagues` and
`PUT /api/leagues/{id}/settings` (omitted keeps the current value), returned on
every league. Existing leagues migrated to 7. The streaming planner will cap
add/drops at it.
