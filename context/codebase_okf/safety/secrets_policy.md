---
type: safety_policy
title: Secrets Policy
description: How credentials, API keys, and tokens are stored, loaded, and kept out of the repository and logs.
tags: [safety, secrets, configuration]
source_paths: [src/FantasyBasketball.Api/Options, src/FantasyBasketball.Api/Program.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Configuration]
depends_on: []
status: implemented
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - No secret value exists anywhere in git history.
  - A missing required secret fails at startup with a named error.
---

# Responsibility

Owns credential handling. This is not a security-focused product, but the basic
hygiene is non-negotiable — the app touches a third-party API key today and
provider OAuth tokens later.

# Rules

- **Never store a fantasy-provider password.** Not encrypted, not hashed, not
  "temporarily". Where a provider needs user auth, it is OAuth or it is manual
  entry.
- **Secrets never enter the repository.** `appsettings.json` holds structure and
  non-secret defaults only. Real values come from .NET user-secrets in
  development and environment variables in deployment.
- **`.gitignore` covers** `appsettings.*.Local.json`, `.env`, `*.pfx`, `secrets/`.
- **Fail fast at startup.** Required configuration is validated on boot with
  `ValidateOnStart`, and the error names the missing key — never a null
  reference at first use, halfway through an import.
- **Secrets never reach logs.** The balldontlie key, connection strings, and any
  future OAuth token are excluded from structured logging and from error
  responses. See [api_surface](../contracts/api_surface.md) on `internal_error`.
- **Provider tokens, when they exist, are encrypted at rest** using ASP.NET Core
  Data Protection. Post-MVP, but the rule is written now so the first
  implementation has no excuse.
- **Rotate anything exposed.** A key committed by accident is a compromised key
  even after the commit is removed — history is public once pushed.

# MVP secrets

| Secret | Key | Source |
|---|---|---|
| balldontlie API key | `BallDontLie:ApiKey` | user-secrets / env |
| Postgres connection | `ConnectionStrings:Fantasy` | user-secrets / env; compose defaults for local only |

Local `compose.yaml` credentials are development-only, are not real secrets, and
are never reused for anything reachable from a network.

# Invariants

- **A missing required secret fails startup with a named error.** *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row
  A-15 boots the host without the key and asserts the failure names
  `BallDontLie:ApiKey`.*
- **No secret literal in the repository.** *Check: the secret scan in
  [run_quality_gates](../tasks/run_quality_gates.md), which fails the gate on any
  hit.*
- **Secrets never appear in a response body or log line.** *Check: row A-10
  (error bodies) and a logging test asserting the key's value never appears in
  captured log output.*
- **Configuration is bound through the Options pattern**, never read via raw
  `IConfiguration["..."]` indexing scattered through services.

# Verification

`test_matrix_api_persistence.md` row A-15, plus the secret scan in the gate.

# Implementation evidence

`BallDontLieOptions` binds the documented section, validates on start, and the
named client reads the key through `IOptions` only when constructed. The test
with an empty configuration asserts the failure names `BallDontLie:ApiKey`;
recorded provider tests use a terminal fixture handler and no real key.
Exception middleware logs only the exception type and trace id, never its
message, and the HTTP test forces SQL/stack/connection-string-shaped text then
asserts none reaches the envelope. The repository secret scan remains green.
No secret-handling rule was weakened.
