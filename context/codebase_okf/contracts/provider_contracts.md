---
type: contract
title: Provider Contracts
description: Canonical data source names, the provider interface signatures, and the fallback ladder every source category must satisfy.
tags: [contract, providers, abstractions]
source_paths: [src/FantasyBasketball.Application/Abstractions]
test_paths: [tests/FantasyBasketball.IntegrationTests/Providers]
depends_on: [provenance_contract.md, player_identity_contract.md]
status: planned
last_updated: 2026-07-29
owners: [engineering]
risk_level: medium
done_criteria:
  - Every external source implements one of these interfaces and nothing else.
  - No provider name string appears outside the canonical list below.
  - Every source category still functions with its primary provider disabled.
---

# Responsibility

Owns the seam between the application and the outside world. The single most
important architectural rule in this project — *the domain must never know
whether data came from an API, a scraper, a CSV, or a human* — is enforced here:
these interfaces are the only way external data enters.

# Canonical source names

The exact strings stored in `DataProvenance.Source` and
`ExternalPlayerIdentity.Provider`. No other spelling is legal.

```text
balldontlie
basketball-reference
fantasypros
csv
manual
```

Role assignment is in [`stack_config.toml`](../../../stack_config.toml)
`[data_sources]`, which is canonical for *which source fills which role*.

# Interfaces

Live in `FantasyBasketball.Application/Abstractions`. Every method takes a
`CancellationToken` as its last parameter — no exceptions.

```csharp
public interface IDataSource
{
    string Name { get; }                    // one of the canonical names above
    DataSourceKind Kind { get; }            // Api | Scraper | File | Manual
}

public interface IPlayerStatsProvider : IDataSource
{
    Task<IReadOnlyList<SeasonStatLine>> GetSeasonStatsAsync(
        int seasonEndYear, CancellationToken ct);
}

public interface IPlayerDirectoryProvider : IDataSource
{
    Task<IReadOnlyList<ExternalPlayer>> GetPlayersAsync(CancellationToken ct);
    Task<IReadOnlyList<ExternalTeam>> GetTeamsAsync(CancellationToken ct);
}

public interface IScheduleProvider : IDataSource
{
    Task<IReadOnlyList<NbaGame>> GetGamesAsync(
        DateOnly from, DateOnly to, CancellationToken ct);
}

public interface IAdpProvider : IDataSource
{
    Task<IReadOnlyList<AdpEntry>> GetAdpAsync(CancellationToken ct);
}

public interface INewsProvider : IDataSource
{
    Task<IReadOnlyList<NewsItem>> GetRecentNewsAsync(CancellationToken ct);
}

public interface IFantasyLeagueProvider : IDataSource
{
    Task<FantasyLeagueSnapshot> GetLeagueAsync(string leagueId, CancellationToken ct);
}
```

`ExternalPlayer`, `ExternalTeam`, and `AdpEntry` are provider-shaped DTOs
carrying an `ExternalId` plus raw names — they are resolved to canonical models
by [player_identity_contract](player_identity_contract.md), not by the provider.

# MVP implementations

| Interface | Implementation | Notes |
|---|---|---|
| `IPlayerDirectoryProvider` | `BallDontLieProvider` | Free tier: teams, players |
| `IScheduleProvider` | `BallDontLieProvider` | Free tier: games |
| `IPlayerStatsProvider` | `BasketballReferenceStatsScraper` | Season pages only |
| `IAdpProvider` | `FantasyProsAdpScraper`, `CsvAdpImporter`, manual entry | Three implementations, one contract |
| `INewsProvider` | *none in MVP* | Context events are user-created — see [context_event_catalog](context_event_catalog.md) |
| `IFantasyLeagueProvider` | *none in MVP* | ESPN/Yahoo/Sleeper are post-MVP optional adapters |

**No interface is created without an implementation.** `INewsProvider` and
`IFantasyLeagueProvider` are declared because the ingestion pipeline's registry
is typed over them and the fallback ladder is a real contract — but if writing
one costs more than it earns at build time, leave it out and add it with its
first implementation. Do not create empty adapter classes to fill the table.

# Invariants

- **The domain project references none of these.** They live in Application;
  Domain has zero knowledge of them. *Check: the architecture test in
  [test_matrix_api_persistence](../tests/test_matrix_api_persistence.md) row A-01.*
- **Every provider result carries provenance** before it is persisted. *Check:
  row I-07.*
- **Every source category degrades, never fails.** A dead provider marks its
  `DataImportRun` failed, lowers confidence, and surfaces staleness — it does
  not throw out of a request. *Check: row I-06.*
- **ADP has three rungs** — scraper, CSV, manual — and must still produce a value
  with the scraper disabled. This is the design doc's source-priority ladder
  made real for the one source that needs it. *Check: row I-08.*
- **Provider names come from the list above**, never spelled inline. *Check: the
  canonical-value grep.*
- **Cancellation is honored.** Every provider passes its token to every await.
  *Check: row I-09 cancels mid-import and asserts no partial commit.*

# Change procedure

Adding a provider: follow [add_new_data_source](../tasks/add_new_data_source.md).
Adding a *name* means adding it here first, then to `[data_sources]` if it takes
a role.

# Verification

Contract tests replay recorded provider payloads through each adapter and assert
the canonical model output — `test_matrix_ingestion_scrapers.md`, rows I-01
through I-09.
