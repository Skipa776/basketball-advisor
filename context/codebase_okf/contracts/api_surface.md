---
type: contract
title: API Surface Contract
description: The response envelope, error codes, validation semantics, and per-endpoint behavior for the HTTP API.
tags: [contract, api, http]
source_paths: [src/FantasyBasketball.Api/Endpoints]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [recommendation_evidence_contract.md, scoring_rules_catalog.md]
status: implemented
last_updated: 2026-07-29
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
