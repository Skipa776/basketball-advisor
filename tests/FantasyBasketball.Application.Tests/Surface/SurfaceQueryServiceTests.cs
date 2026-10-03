using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Context;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Application.Health;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Surface;

public sealed class SurfaceQueryServiceTests
{
    [Fact]
    public async Task League_service_persists_and_replaces_points_scoring()
    {
        var repository = new FakeLeagueRepository();
        var service = new LeagueService(repository, new FakeDraftRepository());
        var league = PointsLeague();

        (await service.CreateAsync(
            league,
            TestContext.Current.CancellationToken)).ShouldBe(league);
        (await service.GetAsync(
            league.Id,
            TestContext.Current.CancellationToken)).ShouldBe(league);

        var replacement = await service.ReplaceScoringAsync(
            league.Id,
            [new ScoringRule(StatKey.PTS, 2m)],
            TestContext.Current.CancellationToken);

        replacement.ScoringRules.Single().PointsPerUnit.ShouldBe(2m);
        repository.Saved.ShouldBe(replacement);
    }

    [Fact]
    public async Task League_service_rejects_missing_or_category_scoring_replacement()
    {
        var repository = new FakeLeagueRepository();
        var service = new LeagueService(repository, new FakeDraftRepository());
        await Should.ThrowAsync<ResourceNotFoundException>(() =>
            service.GetAsync(
                Guid.NewGuid(),
                TestContext.Current.CancellationToken));

        var category = CategoryLeague();
        await service.CreateAsync(
            category,
            TestContext.Current.CancellationToken);
        await Should.ThrowAsync<ResourceConflictException>(() =>
            service.ReplaceScoringAsync(
                category.Id,
                [new ScoringRule(StatKey.PTS, 1m)],
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Player_and_review_queries_validate_paging_and_delegate()
    {
        var player = Player();
        var players = new FakePlayerRepository(player);
        var playerQueries = new FakePlayerQueryRepository(player);
        var playerService = new PlayerQueryService(players, playerQueries);

        (await playerService.GetAsync(
            player.Id,
            TestContext.Current.CancellationToken)).ShouldBe(player);
        var page = await playerService.ListAsync(
            "test",
            "POR",
            "G",
            2,
            25,
            TestContext.Current.CancellationToken);
        page.Items.ShouldBe([player]);
        playerQueries.LastPage.ShouldBe(2);
        playerQueries.LastLimit.ShouldBe(25);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            playerService.ListAsync(
                null,
                null,
                null,
                0,
                25,
                TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            playerService.ListAsync(
                null,
                null,
                null,
                1,
                Paging.MaximumLimit + 1,
                TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ResourceNotFoundException>(() =>
            playerService.GetAsync(
                new PlayerId(Guid.NewGuid()),
                TestContext.Current.CancellationToken));

        var contextQuery = new FakeContextEventQueryRepository();
        var contextService = new ContextEventQueryService(contextQuery);
        var contextPage = await contextService.ListAsync(
            1,
            Paging.DefaultLimit,
            TestContext.Current.CancellationToken);
        contextPage.Total.ShouldBe(0);
        contextQuery.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Import_run_query_places_active_runs_before_persisted_runs()
    {
        var persisted = Run(DataImportRunStatus.Succeeded);
        var active = Run(DataImportRunStatus.Running);
        var service = new ImportRunQueryService(
            new FakeImportRunQueryRepository(persisted),
            new FakeImportJobQueue(active));

        var page = await service.ListAsync(
            1,
            10,
            TestContext.Current.CancellationToken);

        page.Items.Select(run => run.Id).ShouldBe([active.Id, persisted.Id]);
        page.Total.ShouldBe(2);
    }

    [Fact]
    public async Task Draft_board_filters_picks_and_persists_degraded_evidence()
    {
        var league = PointsLeague();
        var session = new DraftSession(Guid.NewGuid(), 10, 13, 1);
        var drafted = session.MakePick(new PlayerId(Guid.NewGuid()));
        var available = new PlayerId(Guid.NewGuid());
        var draftRepository = new FakeDraftRepository(
            new DraftSessionRecord(session, league.Id));
        var recommendationRepository = new FakeRecommendationRepository();
        var failed = new DataImportRun(
            Guid.NewGuid(),
            DataSourceName.BallDontLie,
            DataImportRunStatus.Failed,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            0,
            0,
            "failure");
        var service = new DraftBoardService(
            draftRepository,
            new FakeLeagueRepository(league),
            new FakeDraftCandidateRepository(
            [
                Candidate(drafted.PlayerId, 900m),
                Candidate(available, 800m),
            ]),
            new DraftBoard(new DraftValueCalculator(new DraftWeightOptions())),
            new DraftRecommendationEngine(new ConfidenceCalculator()),
            new DataSourceHealthService(
                new FakeImportRunQueryRepository(failed),
                new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddDays(3)),
                new DataSourceHealthOptions()),
            recommendationRepository,
            new FantasyBasketball.Application.Tests.Backtest.FakeGameRepository(),
            new FantasyBasketball.Application.Tests.Backtest.FakeModelVersionRepository(),
            new DraftSimulator(new SimulationOptions()),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddDays(3)));

        var board = await service.GetBoardAsync(
            session.Id,
            TestContext.Current.CancellationToken);
        board.Rankings.Single().PlayerId.ShouldBe(available);

        var recommendations = await service.GetRecommendationsAsync(
            session.Id,
            TestContext.Current.CancellationToken);
        recommendations.Single().Evidence.ShouldContain(
            evidence => evidence.Kind == EvidenceKind.DataQuality
                && evidence.Polarity == EvidencePolarity.Risk);
        recommendationRepository.Saved.ShouldBe(recommendations);
    }

    [Fact]
    public async Task D15_simulated_board_needs_distributions_and_a_full_schedule()
    {
        var team = new NbaTeamId(Guid.NewGuid());
        var withSpread = Enumerable.Range(1, 40).Select(rank => Simulatable(rank, team)).ToArray();
        var plain = withSpread.Select(candidate => candidate with { Distribution = null }).ToArray();

        var noSpread = await SimulationService(plain, Season(team)).SimulateAsync(DraftId, RiskMode.Mean, TestContext.Current.CancellationToken);
        var noSchedule = await SimulationService(withSpread, []).SimulateAsync(DraftId, RiskMode.Mean, TestContext.Current.CancellationToken);
        var simulated = await SimulationService(withSpread, Season(team)).SimulateAsync(DraftId, RiskMode.Cautious, TestContext.Current.CancellationToken);

        noSpread.Board.ShouldBeNull();
        noSpread.Unavailable.ShouldNotBeNull().ShouldContain("distributions");
        noSchedule.Unavailable.ShouldNotBeNull().ShouldContain("schedule");
        var board = simulated.Board.ShouldNotBeNull();
        board.Mode.ShouldBe(RiskMode.Cautious);
        board.Candidates.Count.ShouldBe(15);
        board.NextUserPick.ShouldBe(20);
    }

    [Fact]
    public async Task D16_recommendations_lead_with_the_simulated_pick_and_its_survival_odds()
    {
        var team = new NbaTeamId(Guid.NewGuid());
        var pool = Enumerable.Range(1, 40).Select(rank => Simulatable(rank, team)).ToArray();
        var service = SimulationService(pool, Season(team));

        var simulated = (await service.SimulateAsync(DraftId, RiskMode.Mean, TestContext.Current.CancellationToken)).Board!;
        var recommendations = await service.GetRecommendationsAsync(DraftId, TestContext.Current.CancellationToken);

        recommendations[0].SubjectPlayerId.ShouldBe(simulated.Candidates[0].PlayerId);
        recommendations[0].Evidence.ShouldContain(item => item.Statement.StartsWith("Best simulated pick", StringComparison.Ordinal));
        recommendations[1].Evidence.ShouldContain(item => item.Statement.StartsWith("About ", StringComparison.Ordinal));
        recommendations.Count.ShouldBe(40, "the rest of the pool follows in heuristic order");
    }

    private static readonly Guid DraftId = Guid.NewGuid();

    private static DraftBoardService SimulationService(IReadOnlyList<DraftCandidate> pool, NbaGame[] games)
    {
        var league = PointsLeague();
        var session = new DraftSession(DraftId, 10, 13, 1);
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));
        return new DraftBoardService(
            new FakeDraftRepository(new DraftSessionRecord(session, league.Id)),
            new FakeLeagueRepository(league),
            new FakeDraftCandidateRepository(pool),
            new DraftBoard(new DraftValueCalculator(new DraftWeightOptions())),
            new DraftRecommendationEngine(new ConfidenceCalculator()),
            new DataSourceHealthService(new FakeImportRunQueryRepository(), clock, new DataSourceHealthOptions()),
            new FakeRecommendationRepository(),
            new FantasyBasketball.Application.Tests.Backtest.FakeGameRepository(games),
            new FantasyBasketball.Application.Tests.Backtest.FakeModelVersionRepository(),
            new DraftSimulator(new SimulationOptions { Rollouts = 100 }),
            clock);
    }

    private static DraftCandidate Simulatable(int rank, NbaTeamId team) => new(
        new PlayerId(Guid.NewGuid()),
        (50m - rank) * 70m,
        [new[] { "PG", "SG", "SF", "PF", "C" }[rank % 5]],
        rank,
        0m,
        0m,
        0m,
        new Dictionary<StatKey, decimal>(),
        Distribution: new SeasonValueDistribution(50m - rank, 5m, new FantasyBasketball.Domain.Statistics.BetaBinomial(82, 6m, 1m)),
        TeamId: team);

    /// <summary>1,230 games from 2026-10-20: the player's team plays every third day, the rest fill the slate.</summary>
    private static NbaGame[] Season(NbaTeamId team) => [.. Enumerable.Range(0, 1230).Select(index => new NbaGame(
        Guid.NewGuid(), 2027, new DateTimeOffset(2026, 10, 20, 23, 0, 0, TimeSpan.Zero).AddDays(index / 8),
        index % 24 == 0 ? team : new NbaTeamId(Guid.NewGuid()), new NbaTeamId(Guid.NewGuid()), null, null, "scheduled",
        new DataProvenance(DataSourceName.BallDontLie, null, DateTimeOffset.UnixEpoch, null, "balldontlie-v1", 1m, new string('a', 64))))];

    private static FantasyLeague PointsLeague() =>
        new(
            Guid.NewGuid(),
            "Points",
            LeagueType.Points,
            10,
            [new ScoringRule(StatKey.PTS, 1m)],
            [],
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);

    private static FantasyLeague CategoryLeague() =>
        new(
            Guid.NewGuid(),
            "Categories",
            LeagueType.Categories,
            10,
            [],
            [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);

    private static Player Player() =>
        new(
            new PlayerId(Guid.NewGuid()),
            "Test Player",
            "test player",
            null,
            ["G"],
            null);

    private static DraftCandidate Candidate(PlayerId id, decimal value) =>
        new(
            id,
            value,
            ["G"],
            20m,
            0m,
            0m,
            0m,
            new Dictionary<StatKey, decimal>());

    private static DataImportRun Run(DataImportRunStatus status) =>
        new(
            Guid.NewGuid(),
            DataSourceName.Manual,
            status,
            DateTimeOffset.UnixEpoch,
            status == DataImportRunStatus.Running
                ? null
                : DateTimeOffset.UnixEpoch,
            1,
            0,
            null);

    private sealed class FakeLeagueRepository : ILeagueRepository
    {
        private readonly Dictionary<Guid, FantasyLeague> leagues = [];

        public FakeLeagueRepository(FantasyLeague? league = null)
        {
            if (league is not null)
            {
                leagues.Add(league.Id, league);
            }
        }

        public FantasyLeague? Saved { get; private set; }

        public Task AddAsync(
            FantasyLeague league,
            CancellationToken cancellationToken)
        {
            leagues.Add(league.Id, league);
            return Task.CompletedTask;
        }

        public Task<FantasyLeague?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(leagues.GetValueOrDefault(id));

        public Task<IReadOnlyList<FantasyLeague>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<FantasyLeague>>(
                [.. leagues.Values.OrderBy(value => value.Name)]);

        public Task SaveScoringAsync(
            FantasyLeague league,
            CancellationToken cancellationToken)
        {
            Saved = league;
            leagues[league.Id] = league;
            return Task.CompletedTask;
        }

        public Task SaveSettingsAsync(
            FantasyLeague league,
            CancellationToken cancellationToken)
        {
            Saved = league;
            leagues[league.Id] = league;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePlayerRepository(Player player) : IPlayerRepository
    {
        public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Player?> GetAsync(
            PlayerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(id == player.Id ? player : null);

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Player?>(null);

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>([]);

        public Task AddAsync(
            Player value,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExternalPlayerIdentity?>(null);

        public Task AddResolvedIdentityAsync(
            Player value,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakePlayerQueryRepository(Player player)
        : IPlayerQueryRepository
    {
        public int LastPage { get; private set; }

        public int LastLimit { get; private set; }

        public Task<PagedResult<Player>> ListAsync(
            string? search,
            string? team,
            string? position,
            int page,
            int limit,
            CancellationToken cancellationToken)
        {
            LastPage = page;
            LastLimit = limit;
            return Task.FromResult(new PagedResult<Player>(
                [player],
                1,
                page,
                limit));
        }
    }

    private sealed class FakeContextEventQueryRepository
        : IContextEventQueryRepository
    {
        public int Calls { get; private set; }

        public Task<PagedResult<ContextEvent>> ListAsync(
            int page,
            int limit,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new PagedResult<ContextEvent>(
                [],
                0,
                page,
                limit));
        }
    }

    private sealed class FakeImportRunQueryRepository(
        params DataImportRun[] runs) : IImportRunQueryRepository
    {
        public Task<PagedResult<DataImportRun>> ListAsync(
            int page,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<DataImportRun>(
                runs,
                runs.Length,
                page,
                limit));

        public Task<IReadOnlyList<DataImportRun>> ListRecentAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DataImportRun>>(runs);
    }

    private sealed class FakeImportJobQueue(params DataImportRun[] active)
        : IImportJobQueue
    {
        public Task<DataImportRun> EnqueueAsync(
            ImportJobRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(active[0]);

        public IReadOnlyList<DataImportRun> GetActive() => active;
    }

    private sealed class FakeDraftRepository(DraftSessionRecord? record = null)
        : IDraftRepository
    {
        public Task<PagedResult<DraftSessionRecord>> ListAsync(
            Guid leagueId,
            int page,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<DraftSessionRecord>(
                record is not null && record.LeagueId == leagueId ? [record] : [],
                record is not null && record.LeagueId == leagueId ? 1 : 0,
                page,
                limit));

        public Task<bool> HasAnyForLeagueAsync(
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.FromResult(record?.LeagueId == leagueId);

        public Task AddSessionAsync(
            DraftSession session,
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<DraftSessionRecord?> GetSessionAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(record?.Session.Id == id ? record : null);

        public Task AddPickAsync(
            DraftPick pick,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveLastPickAsync(
            Guid draftSessionId,
            int pickNumber,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeDraftCandidateRepository(
        IReadOnlyList<DraftCandidate> candidates) : IDraftCandidateRepository
    {
        public Task<IReadOnlyList<DraftCandidate>> ListAsync(
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.FromResult(candidates);
    }

    private sealed class FakeRecommendationRepository
        : IRecommendationRepository
    {
        public IReadOnlyList<Recommendation>? Saved { get; private set; }

        public Task AddRangeAsync(
            IReadOnlyList<Recommendation> recommendations,
            CancellationToken cancellationToken)
        {
            Saved = recommendations;
            return Task.CompletedTask;
        }

        public Task<Recommendation?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Recommendation?>(null);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
