---
type: safety_policy
title: Self-Host Hardening
description: Shipping defaults that are safe when someone exposes this to a network, and an explicit split of what the operator owns.
tags: [safety, deployment, hardening, self-host]
source_paths: [compose.yaml, Dockerfile, src/FantasyBasketball.Api/Program.cs]
test_paths: [tests/FantasyBasketball.IntegrationTests/Configuration]
depends_on: [secrets_policy.md, ../contracts/auth_tenancy_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: high
edit_policy: stable_contract
done_criteria:
  - A clean clone runs with no secrets and is not reachable from off-host by default.
  - No shipped artifact contains a default credential.
  - What the operator must do themselves is written down, not assumed.
---

# Responsibility

Owns the defaults for a product other people run. Self-hosted software is deployed
by someone who has not read the code, on a box that may be more exposed than they
intend. **The shipping defaults have to be safe when the operator does nothing.**

# Shipping defaults

| Concern | Default |
|---|---|
| Bind address | Loopback. Exposing it is an explicit compose or config edit |
| HTTPS | Required outside `Development`; HSTS on. Starting with a non-loopback bind and no TLS **fails at boot with a named error** |
| Registration | Open until the first user claims the instance, then closed unless `Auth:OpenRegistration = true` |
| Default credentials | **None, anywhere.** No seeded admin, no default password, no bootstrap token |
| Postgres port | Not published to the host in the shipped compose file |
| `/metrics` | Loopback-only unless explicitly opened; never public with the app |
| Container user | Non-root, read-only root filesystem, no added capabilities |
| Demo data | Opt-in via a flag, and clearly labelled as fictional in the UI |
| LLM | Disabled; no key, no calls, full functionality |

The no-default-credential rule is the one that matters most: every self-hosted
breach story starts with a credential that shipped in the artifact. The instance
is claimed by whoever registers first, and that window is the operator's to close
in the minute after `docker compose up`.

`compose.yaml`'s Postgres password is a development value, is generated on first
run rather than hardcoded, and the file says in a comment that it is not for
anything reachable from a network.

# Upgrade path

- Image tags are immutable; `latest` exists but the README pins a version.
- Migrations are forward-only and apply on start when `Database:MigrateOnStart` is
  set, which the compose quickstart sets and a production deployment may not.
- **A migration never destroys user data to succeed.** A migration that cannot
  preserve data fails and says what to back up first.
- The README documents `pg_dump` before upgrading, because nobody reads it after.

# What the operator owns

Written down because pretending otherwise is the actual risk:

- **The network boundary.** Reverse proxy, TLS certificates, firewall. The app
  refuses plaintext off-loopback but cannot audit the proxy in front of it.
- **Host and image patching**, and the base-image rebuild cadence.
- **Backups and restore testing.** The app can be re-imported from sources; a
  user's manual league configuration and context events cannot.
- **API key custody.** balldontlie and any Anthropic key are the operator's, with
  the operator's rate limits and the operator's bill.
- **Terms-of-use compliance** for every scrape target, per
  [scraping_policy](scraping_policy.md) — a human judgment no check replaces.
- **Whether to expose the instance at all.** Closed registration plus HTTPS makes
  exposure defensible; it does not make it necessary.

# Invariants

- **A clean clone boots with no secrets configured** and serves the app on
  loopback. *Check: [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md)
  row A-22 boots from default configuration only.*
- **Non-loopback bind without TLS fails at boot, naming the setting.** *Check: row A-23.*
- **No credential literal in any shipped artifact.** *Check: the secret scan,
  extended to `compose.yaml`, the `Dockerfile`, and `appsettings*.json`.*
- **The image runs as non-root with a read-only root filesystem.** *Check: row A-24
  inspects the built image.*
- **Postgres is not published to the host** in the shipped compose. *Check: row A-25
  parses `compose.yaml`.*
- **Demo data is opt-in and labelled.** *Check: row A-26.*
- **Migrations apply forward on a populated volume without data loss.** *Check: row
  A-27 seeds, upgrades, and asserts the rows survive.*
- **`/metrics` is not reachable from off-host by default.** *Check: row A-28.*

# Change procedure

`stable_contract`. Changing a default toward *less* safe — publishing a port,
relaxing TLS, seeding a credential, opening registration — requires explicit user
approval and an [assumptions](../assumptions.md) entry. Changing one toward more
safe needs only a passing gate.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows A-22
through A-28, plus the extended secret scan.
