---
type: component
title: API Host
description: The ASP.NET Core host — DI wiring, middleware, options binding, and logging.
tags: [component, api, aspnetcore]
source_paths: [src/FantasyBasketball.Api]
test_paths: [tests/FantasyBasketball.IntegrationTests/Api]
depends_on: [../contracts/api_surface.md, ../safety/secrets_policy.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - The host is the only project that wires DI.
  - A missing required setting fails startup with a named error.
---

# Responsibility

Owns composition: the one place where interfaces meet implementations, plus
middleware, configuration, and logging. Routes and semantics are owned by
[api_surface](../contracts/api_surface.md).

# Design

- Minimal APIs grouped into `Endpoints/*.cs` extension methods, one per resource
  — not one 500-line `Program.cs`.
- `DependencyInjection.cs` holds registration extension methods per layer:
  `AddDomain()`, `AddApplication()`, `AddInfrastructure(config)`.
- **Options pattern with validation**: every options class is bound, validated
  with data annotations, and registered `ValidateOnStart` so misconfiguration
  fails at boot rather than mid-import.
- **Middleware order**: exception handling outermost, then request logging, then
  routing. Exception handling must wrap everything or an error escapes the
  envelope.
- **Logging** is built-in `ILogger` with structured message templates
  (`"Imported {RowCount} rows from {Source}"` — named holes, never string
  interpolation, or the structure is lost).
- Blazor Server components are hosted by this same project — one host, one port,
  no CORS to configure.

# Invariants

- **Only this project wires DI.** No `IServiceCollection` extension in Domain or
  Application that registers concrete Infrastructure types. *Check:
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-18.*
- **Every exception reaches the client as an envelope**, never a stack trace.
  *Check: rows A-10 and A-14.*
- **Missing required configuration fails startup with the key named.** *Check:
  row A-15.*
- **Log messages use structured templates**, never interpolated strings.
  *Check: a scan for `LogInformation($` and friends.*
- **No secret is ever logged.** *Check: the logging assertion in
  [secrets_policy](../safety/secrets_policy.md).*
- **Every endpoint takes and forwards the request `CancellationToken`.**
  *Check: row A-19.*

# Change procedure

Adding an endpoint group: the endpoint class, its registration, its contract
entry if semantics are non-obvious, and an integration test — one commit.

# Verification

[test_matrix_api_persistence](../tests/test_matrix_api_persistence.md), rows
A-10, A-14, A-15, A-18, A-19.
