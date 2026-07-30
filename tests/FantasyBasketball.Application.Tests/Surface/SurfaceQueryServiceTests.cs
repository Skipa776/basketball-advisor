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
        var service = new LeagueService(repository);
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
        var service = new LeagueService(repository);
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
            recommendationRepository);

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
    }

    private sealed class FakePlayerRepository(Player player) : IPlayerRepository
    {
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

    private sealed class FakeDraftRepository(DraftSessionRecord record)
        : IDraftRepository
    {
        public Task AddSessionAsync(
            DraftSession session,
            Guid leagueId,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<DraftSessionRecord?> GetSessionAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(id == record.Session.Id ? record : null);

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
