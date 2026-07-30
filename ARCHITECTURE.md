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

---

# Post-MVP subsystems (R11–R22)

Added by the epic that needs them, in `docs/epics/` order. Same rule as above:
these paths appear when their subsystem is built and tested.

```text
src/FantasyBasketball.Domain/
  Trends/           RollingWindow.cs  WindowSpan.cs  TrendScore.cs
                    ProductionDecomposition.cs  Sustainability.cs
  Categories/       CategoryZScore.cs  TeamCategoryProfile.cs  PuntProfile.cs
                    MatchupOutlook.cs  NormalDistribution.cs
  Draft/            SurvivalProbability.cs  Tier.cs
  Streaming/        UsableGame.cs  LineupDay.cs  StreamingPlan.cs  StreamingMove.cs
  Trades/           TradeProposal.cs  TradeEvaluation.cs
  Accounts/         UserId.cs  OwnedResource.cs

src/FantasyBasketball.Application/
  Trends/           TrendAnalysisService.cs  WindowBuilder.cs
  Categories/       CategoryAnalyzerService.cs  PuntDetector.cs
  Draft/            SurvivalModel.cs  TierDetector.cs
  Streaming/        UsableGameCalculator.cs  StreamingPlanner.cs
  Trades/           TradeEvaluationService.cs
  Context/          ContextProposalService.cs        # reviews LLM proposals
  Backtest/         BacktestRunner.cs  AccuracyMetrics.cs  WeightOptimizer.cs
  Abstractions/     IContextEventProposer.cs  IUserContext.cs

src/FantasyBasketball.Infrastructure/
  Scrapers/BasketballReference/  BoxScoreScraper.cs  BoxScoreParser.cs
  Providers/Yahoo/               YahooOAuthClient.cs  YahooLeagueProvider.cs
  Providers/Sleeper/             SleeperLeagueProvider.cs
  Import/                        CsvLeagueImporter.cs
  Llm/                           ClaudeContextProposer.cs  ExtractionPrompt.cs
                                 ProposalSchema.cs  TokenBudget.cs
  Identity/                      FantasyUser.cs  IdentityDbConfiguration.cs
  Workers/                       BoxScoreImportWorker.cs  TrendRefreshWorker.cs
  Telemetry/                     Metrics.cs

src/FantasyBasketball.Api/
  Components/Pages/  Trends.razor  FreeAgents.razor  Streamers.razor
                     Trades.razor  Matchup.razor  Account/*.razor
  Components/Design/ Tokens.razor.css  <design-system components>

tests/
  FantasyBasketball.Domain.Tests/{Trends,Categories,Streaming,Trades}
  FantasyBasketball.IntegrationTests/{Auth,Llm,Providers,Backtest}
  FantasyBasketball.IntegrationTests/Fixtures/Articles/   # LLM extraction fixtures
```

## Post-MVP type shapes

Values, formulas, and enum members stay in their `contracts/` catalogs.

```csharp
// --- Trends (R11) ------------------------------------------------------------
// Windows and the trend formula: contracts/rolling_window_contract.md
public sealed record RollingWindow(
    PlayerId PlayerId, WindowSpan Span,
    int GamesInWindow, decimal MinutesPerGame,
    StatLine PerGame, decimal? UsageRate, DateOnly Through);

public sealed record ProductionDecomposition(
    decimal TotalChange,          // change in fantasy points per game
    decimal FromMinutes,          // additive contributions; sum to TotalChange
    decimal FromUsage,
    decimal FromEfficiency,
    decimal OpportunityShare);    // (FromMinutes + FromUsage) / |TotalChange|

public sealed record TrendScore(
    PlayerId PlayerId, WindowSpan Span, decimal Score,
    Sustainability Sustainability, ProductionDecomposition Decomposition,
    decimal SampleWeight,
    IReadOnlyList<RecommendationEvidence> Evidence);

// --- Categories (R12) --------------------------------------------------------
// Z-score math, volume weighting, punt rules: contracts/category_value_contract.md
public sealed record CategoryZScore(StatKey Category, decimal Z, decimal Weight);

public sealed record TeamCategoryProfile(
    Guid LeagueId, Guid RosterId,
    IReadOnlyDictionary<StatKey, decimal> ExpectedWeeklyTotal,
    IReadOnlyDictionary<StatKey, decimal> Percentile,
    IReadOnlyDictionary<StatKey, decimal> WinProbability,
    PuntProfile Punt);

public sealed record PuntProfile(
    IReadOnlyList<StatKey> Punted, bool UserChosen, string Rationale);

// --- Draft intelligence (R13) ------------------------------------------------
// Survival model and tier rule: contracts/draft_intelligence_contract.md
public sealed record SurvivalProbability(
    PlayerId PlayerId, int AtPick, decimal Probability, decimal AdpStdDev);

public sealed record Tier(int Rank, decimal TopValue, decimal BottomValue, int Size);

// --- Streaming (R14) ---------------------------------------------------------
// Usable-game algorithm: contracts/streaming_contract.md
public sealed record LineupDay(
    DateOnly Date, int OpenSlots, int LeagueWideGameCount);

public sealed record UsableGame(
    PlayerId PlayerId, DateOnly Date, bool Usable, decimal ExpectedValue,
    string Reason);                       // why it is or is not usable

public sealed record StreamingMove(
    DateOnly Date, PlayerId? Add, PlayerId? Drop, decimal ExpectedGain);

public sealed record StreamingPlan(
    Guid LeagueId, DateOnly From, DateOnly To,
    IReadOnlyList<StreamingMove> Moves,
    decimal ExpectedIncrementalValue, int AcquisitionsUsed,
    IReadOnlyList<RecommendationEvidence> Evidence);

// --- Trades (R15) ------------------------------------------------------------
public sealed record TradeProposal(
    Guid LeagueId, Guid ProposingRosterId,
    IReadOnlyList<PlayerId> Sending, IReadOnlyList<PlayerId> Receiving);

public sealed record TradeEvaluation(
    TradeProposal Proposal,
    decimal ValueBefore, decimal ValueAfter,
    decimal ReplacementBackfill,
    TeamCategoryProfile? CategoriesBefore, TeamCategoryProfile? CategoriesAfter,
    decimal ExpectedWinsDelta,
    Confidence Confidence,
    IReadOnlyList<RecommendationEvidence> Evidence);

// --- LLM proposals (R17) -----------------------------------------------------
// Prompt, schema, and boundary: contracts/llm_extraction_contract.md
public sealed record ArticleSource(
    string Url, string Title, string Text, DateTimeOffset PublishedAt,
    string ContentHash);

public sealed record ProposedContextEvent(
    ContextEventType Type,
    string? TeamExternalName, string? PrimaryPlayerName,
    IReadOnlyList<string> AffectedPlayerNames,
    ContextDirection Direction, decimal Magnitude, Confidence Confidence,
    string Summary, string SupportingQuote);   // quote must appear in the article

public sealed record ExtractionResult(
    ArticleSource Source, IReadOnlyList<ProposedContextEvent> Proposals,
    string ModelId, int InputTokens, int OutputTokens, DateTimeOffset ExtractedAt);

// --- Back-testing (R18) ------------------------------------------------------
// Metric definitions and protocol: contracts/backtest_contract.md
public sealed record AccuracyMetrics(
    int SeasonEndYear, int PlayerCount,
    decimal MeanAbsoluteError, decimal RootMeanSquaredError,
    decimal SpearmanRho, decimal TopKHitRate, int K,
    IReadOnlyList<decimal> CalibrationByDecile);

public sealed record WeightCandidate(
    decimal Scarcity, decimal Fit, decimal Market, decimal Risk,
    decimal RealizedRosterValue);

// --- Accounts (R20) ---------------------------------------------------------
// Ownership and authorization rules: contracts/auth_tenancy_contract.md
public readonly record struct UserId(Guid Value);
public interface IUserContext { UserId CurrentUser { get; } }
```

Owned resources (`FantasyLeague`, `DraftSession`, `ContextEvent`,
`Recommendation`) gain a non-nullable `OwnerId`. Reference data (`Player`,
`NbaTeam`, `SeasonStatLine`, `NbaGame`, `AdpEntry`, `RollingWindow`,
`BaselineProjection`) is **global and unowned** — see
`safety/tenancy_policy.md` for why that split is the whole design.

## Post-MVP API additions

```text
GET    /api/players/{id}/trends?span=            R11
GET    /api/leagues/{id}/free-agents             R11
GET    /api/leagues/{id}/categories              R12
GET    /api/leagues/{id}/matchup                 R12
GET    /api/drafts/{id}/survival?pick=           R13
GET    /api/drafts/{id}/tiers                    R13
POST   /api/leagues/{id}/streaming-plan          R14
POST   /api/leagues/{id}/trades/evaluate         R15
POST   /api/imports/league                       R16  provider or CSV
POST   /api/context-events/propose               R17  article in, proposals out
GET    /api/backtest/reports                     R18
POST   /api/account/{register,login,logout}      R20
GET    /health/{live,ready}                      R22
GET    /metrics                                  R22
```

