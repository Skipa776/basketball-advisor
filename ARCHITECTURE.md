# ARCHITECTURE.md

Canonical for: **project layout, type shapes, and API routes.**
Not canonical for: **values** — enum members, stat keys, scoring numbers, and
formula weights live in their `context/codebase_okf/contracts/` catalogs and are
referenced from here, never copied.

> **The tree below is a target shape, not a scaffold.** Files appear as their
> subsystem is built and tested. Do not pre-create empty or TODO-only files to
> make the tree match — an absent file is honest, a stub file lies.

---

## Solution layout

```text
FantasyBasketball.sln
global.json                     # SDK 10.0.100, rollForward latestFeature
Directory.Build.props           # net10.0, nullable, TreatWarningsAsErrors
Directory.Packages.props        # central package management; the only place versions appear
compose.yaml                    # postgres:17
.editorconfig
scripts/gate.sh                 # runs [commands] from stack_config.toml in order

src/
  FantasyBasketball.Domain/          # pure. no EF, no HttpClient, no provider names.
    Players/          Player.cs  NbaTeam.cs  ExternalPlayerIdentity.cs  PlayerName.cs
    Stats/            StatKey.cs  StatLine.cs  SeasonStatLine.cs
    Leagues/          FantasyLeague.cs  ScoringRule.cs  RosterSlot.cs  LeagueType.cs
    Scoring/          IScoringEngine.cs  PointsScoringEngine.cs  CategoryScoringEngine.cs
    Projections/      ObservedStats.cs  BaselineProjection.cs  AdjustedProjection.cs
                      FantasyValue.cs  MinutesProjection.cs
    Context/          ContextEvent.cs  ContextEventType.cs  PlayerContextImpact.cs
    Draft/            DraftSession.cs  DraftPick.cs  DraftBoard.cs  DraftValue.cs
                      ReplacementLevel.cs  PositionalScarcity.cs  RosterFit.cs
    Recommendations/  Recommendation.cs  RecommendationEvidence.cs  Confidence.cs
    Provenance/       DataProvenance.cs  DataSourceName.cs

  FantasyBasketball.Application/     # use cases. depends on Domain only.
    Abstractions/     IPlayerStatsProvider.cs  IScheduleProvider.cs  IAdpProvider.cs
                      INewsProvider.cs  IFantasyLeagueProvider.cs  IDataSource.cs
                      IPlayerRepository.cs  ILeagueRepository.cs  IDraftRepository.cs
                      IProjectionRepository.cs  IContextEventRepository.cs
    Leagues/          CreateLeagueService.cs  LeagueValidator.cs
    Ingestion/        ImportPlayersService.cs  ImportSeasonStatsService.cs
                      ImportScheduleService.cs  ImportAdpService.cs
                      PlayerIdentityResolver.cs
    Projections/      ProjectionService.cs  MinutesProjector.cs  ContextApplier.cs
    Draft/            DraftSessionService.cs  DraftRecommendationService.cs
    Context/          ContextEventService.cs
    Health/           DataSourceHealthService.cs

  FantasyBasketball.Infrastructure/  # everything external.
    Persistence/      FantasyDbContext.cs  Configurations/*.cs  Migrations/*.cs
                      Repositories/*.cs
    Http/             ResilientHttpHandler.cs  HostRateLimiter.cs  ResponseCache.cs
    Providers/BallDontLie/     BallDontLieClient.cs  BallDontLieProvider.cs  Dtos/*.cs
    Scrapers/BasketballReference/  BasketballReferenceStatsScraper.cs
                                   SeasonTableParser.cs  BbrefUrlBuilder.cs
    Scrapers/FantasyPros/          FantasyProsAdpScraper.cs  AdpTableParser.cs
                                   FantasyProsUrlBuilder.cs
    Import/           CsvAdpImporter.cs  DataImportRunRecorder.cs
    Workers/          ScheduleRefreshWorker.cs  StatRefreshWorker.cs  AdpRefreshWorker.cs

  FantasyBasketball.Api/             # ASP.NET Core host + Blazor Server UI
    Program.cs  DependencyInjection.cs
    Endpoints/        LeagueEndpoints.cs  PlayerEndpoints.cs  DraftEndpoints.cs
                      ProjectionEndpoints.cs  ContextEndpoints.cs  HealthEndpoints.cs
    Middleware/       ExceptionHandlingMiddleware.cs  RequestLoggingMiddleware.cs
    Options/          BallDontLieOptions.cs  ScrapingOptions.cs  DraftWeightOptions.cs
    Components/       App.razor  Routes.razor  Layout/*.razor
      Pages/          Dashboard.razor  MyLeague.razor  Players.razor
                      DraftAssistant.razor  ContextReview.razor  DataSources.razor

tests/
  FantasyBasketball.Domain.Tests/          # no DB, no network, no I/O
  FantasyBasketball.Application.Tests/     # fakes only
  FantasyBasketball.IntegrationTests/      # Testcontainers Postgres + fixture-backed HTTP
    Fixtures/Html/                         # saved pages for parser tests
```

**Dependency rule.** `Domain` references nothing. `Application` references
`Domain`. `Infrastructure` references `Application` + `Domain`. `Api` references
all three and is the only project that wires DI. Enforced by an architecture
test, not by convention — see `tests/test_matrix_api_persistence.md`.

---

## Core type shapes

Property names and types below are canonical. Enum *members* are not defined
here; each enum links to the catalog that owns its members.

```csharp
// --- Identity -------------------------------------------------------------
public readonly record struct PlayerId(Guid Value);
public readonly record struct NbaTeamId(Guid Value);

public sealed record Player(
    PlayerId Id,
    string FullName,
    string NormalizedName,          // algorithm: contracts/player_identity_contract.md
    NbaTeamId? CurrentTeamId,
    IReadOnlyList<string> Positions,
    DateOnly? BirthDate);

public sealed record ExternalPlayerIdentity(
    PlayerId PlayerId,
    string Provider,                // names: contracts/provider_contracts.md
    string ExternalId,
    DateTimeOffset LinkedAt,
    bool ConfirmedByHuman);

// --- Stats ----------------------------------------------------------------
// StatKey members: contracts/stat_vocabulary.md (the keystone catalog)
public sealed record StatLine(IReadOnlyDictionary<StatKey, decimal> Values)
{
    public decimal this[StatKey key] => Values.TryGetValue(key, out var v) ? v : 0m;
}

public sealed record SeasonStatLine(
    PlayerId PlayerId,
    int SeasonEndYear,              // 2025-26 season => 2026
    int GamesPlayed,
    decimal MinutesPerGame,
    StatLine PerGame,
    StatLine Totals,
    decimal? UsageRate,
    DataProvenance Provenance);

// --- Leagues --------------------------------------------------------------
// LeagueType members and the seed league: contracts/scoring_rules_catalog.md
public sealed record ScoringRule(StatKey Stat, decimal PointsPerUnit);

public sealed record FantasyLeague(
    Guid Id,
    string Name,
    LeagueType Type,
    int TeamCount,
    IReadOnlyList<ScoringRule> ScoringRules,      // points leagues
    IReadOnlyList<StatKey> Categories,            // category leagues
    IReadOnlyList<RosterSlot> RosterSlots,
    LineupCadence Cadence);

// --- Projections (four separate records; never merged, never overwritten) --
// Math: contracts/projection_pipeline_contract.md
public sealed record ObservedStats(PlayerId PlayerId, SeasonStatLine Source, DateTimeOffset AsOf);

public sealed record BaselineProjection(
    Guid Id, PlayerId PlayerId,
    decimal ProjectedMinutesPerGame,
    StatLine PerMinuteRates,
    StatLine ProjectedPerGame,
    int ProjectedGamesPlayed,
    DateTimeOffset ComputedAt,
    string ModelVersion);

public sealed record AdjustedProjection(
    Guid Id, PlayerId PlayerId,
    Guid BaselineProjectionId,                    // immutable reference, never a mutation
    StatLine ProjectedPerGame,
    IReadOnlyList<Guid> AppliedContextEventIds,
    decimal RoleRisk,
    Confidence Confidence,
    DateTimeOffset ComputedAt);

public sealed record FantasyValue(
    PlayerId PlayerId, Guid LeagueId,
    decimal PerGame, decimal SeasonTotal,
    Guid? AdjustedProjectionId);

// --- Context --------------------------------------------------------------
// ContextEventType members + magnitude/confidence scales: contracts/context_event_catalog.md
public sealed record ContextEvent(
    Guid Id, ContextEventType Type,
    NbaTeamId? TeamId, PlayerId? PrimaryPlayerId, IReadOnlyList<PlayerId> AffectedPlayerIds,
    DateTimeOffset CreatedAt, DateTimeOffset EffectiveFrom, DateTimeOffset? ExpectedExpiration,
    ContextDirection Direction, decimal Magnitude, Confidence Confidence,
    string? SourceUrl, string SourceName, string? RawText, string Summary,
    VerificationState Verification);              // never Verified without a human

public sealed record PlayerContextImpact(
    Guid Id, Guid ContextEventId, PlayerId PlayerId,
    decimal MinutesDelta, decimal UsageDelta,
    decimal AssistShareDelta, decimal ReboundShareDelta, decimal ShotVolumeDelta,
    decimal RoleRiskDelta, decimal ProjectionConfidenceDelta);

// --- Draft ----------------------------------------------------------------
// Formula and weights: contracts/draft_value_contract.md
public sealed record DraftValue(
    PlayerId PlayerId, decimal Total,
    decimal ProjectedSeasonValue, decimal ValueAboveReplacement,
    decimal PositionalScarcity, decimal RosterFit, decimal MarketValue,
    decimal ContextAdjustment, decimal InjuryRisk, decimal RoleRisk,
    IReadOnlyList<RecommendationEvidence> Evidence);

// --- Recommendations ------------------------------------------------------
// Confidence members + the evidence contract: contracts/recommendation_evidence_contract.md
public sealed record RecommendationEvidence(
    EvidenceKind Kind, EvidencePolarity Polarity, string Statement, decimal? Magnitude);

public sealed record Recommendation(
    Guid Id, string Action, PlayerId? SubjectPlayerId,
    decimal Score, Confidence Confidence,
    IReadOnlyList<RecommendationEvidence> Evidence);   // must never be empty

// --- Provenance -----------------------------------------------------------
// Field semantics: contracts/provenance_contract.md
public sealed record DataProvenance(
    string Source, string? ExternalId,
    DateTimeOffset FetchedAt, DateTimeOffset? SourceTimestamp,
    string ParserVersion, decimal Confidence, string RawRecordHash);
```

---

## API surface

Minimal APIs, grouped per `Endpoints/*.cs`. Full request/response DTOs and error
envelope: `contracts/api_surface.md`.

```text
POST   /api/leagues                          create a league
GET    /api/leagues/{id}
PUT    /api/leagues/{id}/scoring             replace scoring rules

GET    /api/players?search=&team=&position=
GET    /api/players/{id}
GET    /api/players/{id}/projection?leagueId=   decomposed: observed/baseline/adjusted/value

POST   /api/imports/players                  balldontlie
POST   /api/imports/schedule                 balldontlie
POST   /api/imports/season-stats             basketball-reference
POST   /api/imports/adp                      fantasypros, or CSV body
GET    /api/imports/runs                     DataImportRun history

POST   /api/drafts                           create session
GET    /api/drafts/{id}/board?leagueId=      available pool, re-ranked
POST   /api/drafts/{id}/picks                record a pick
DELETE /api/drafts/{id}/picks/{pickNumber}   undo
GET    /api/drafts/{id}/recommendations      ranked, each with evidence

GET    /api/context-events
POST   /api/context-events                   user-created
POST   /api/context-events/{id}/verify       human confirmation
POST   /api/context-events/{id}/reject

GET    /api/health/data-sources              per-source last success + staleness
```

All responses use one envelope: `{ success, data, error, meta }`
(`contracts/api_surface.md` owns its exact shape).
