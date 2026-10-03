using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using Shouldly;

using FantasyBasketball.Application.Tests.Backtest;

namespace FantasyBasketball.Application.Tests.Projections;

public sealed class LeagueProjectionServiceTests
{
    [Fact]
    public async Task Publication_scores_the_selected_pool_and_preserves_all_four_records()
    {
        var store = new Store();
        var service = Create(store);
        var token = TestContext.Current.CancellationToken;
        (await service.ListPoolsAsync(token)).Count.ShouldBe(1);
        var result = await service.RecalculateAsync(store.League!.Id, 2026, DataSourceName.Manual, token);
        store.TransactionEntered.ShouldBeTrue();
        result.PlayerCount.ShouldBe(1);
        result.ComputedAt.ShouldBe(DateTimeOffset.UnixEpoch);
        store.Observed.ShouldNotBeNull();
        store.Observed.Source.ShouldBe(store.Pool[0]);
        store.Baseline.ShouldNotBeNull();
        store.Adjusted.ShouldNotBeNull();
        store.Adjusted.BaselineProjectionId.ShouldBe(store.Baseline.Id);
        store.Value.ShouldNotBeNull();
        store.Value.AdjustedProjectionId.ShouldBe(store.Adjusted.Id);
        store.Value.PerGame.ShouldBe(new PointsScoringEngine().Score(store.Adjusted.ProjectedPerGame, store.League));
        store.Value.SeasonTotal.ShouldBe(store.Value.PerGame * store.Baseline.ProjectedGamesPlayed);
        store.Profile.ShouldBe(store.League);
        store.PublicationId.ShouldNotBeNull();
        store.Tokens.ShouldAllBe(value => value == token);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("categories")]
    [InlineData("empty")]
    [InlineData("cancelled")]
    public async Task Invalid_or_cancelled_publication_never_writes_a_projection(string reason)
    {
        var store = new Store();
        var id = store.League!.Id;
        using var cancellation = new CancellationTokenSource();
        if (reason == "missing") store.League = null;
        if (reason == "categories") store.League = new FantasyLeague(id, "Categories", LeagueType.Categories, 7, [], [StatKey.PTS], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        if (reason == "empty") store.Pool = [];
        if (reason == "cancelled") cancellation.Cancel();
        var service = Create(store);
        var exception = await Record.ExceptionAsync(() => service.RecalculateAsync(id, 2026, DataSourceName.Manual, cancellation.Token));
        exception.ShouldNotBeNull();
        if (reason == "missing") exception.ShouldBeOfType<ResourceNotFoundException>();
        else if (reason == "cancelled") exception.ShouldBeOfType<OperationCanceledException>();
        else exception.ShouldBeOfType<ResourceConflictException>();
        store.Baseline.ShouldBeNull();
        store.Value.ShouldBeNull();
    }

    private static LeagueProjectionService Create(Store store)
    {
        var options = new ProjectionOptions();
        var clock = new FixedClock();
        return new LeagueProjectionService(store, store,
            new ProjectionService(new BaselineProjector(new MinutesProjector(), options), store, options, clock,
                new FakeModelVersionRepository(), store, new FakePlayerRepository()),
            store, store, new ContextApplier(new ConfidenceCalculator()), new PointsScoringEngine(), store, clock);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class Store : ILeagueRepository, ISeasonStatLineRepository, IProjectionRepository, IContextEventRepository, IImportTransaction
    {
        public FantasyLeague? League { get; set; } = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        public IReadOnlyList<SeasonStatLine> Pool { get; set; } = [new(new PlayerId(Guid.NewGuid()), 2026, 40, 30m,
            new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = 20m }),
            new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = 800m, [StatKey.MIN] = 1200m }), null,
            new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)))];
        public bool TransactionEntered { get; private set; }
        public ObservedStats? Observed { get; private set; }
        public BaselineProjection? Baseline { get; private set; }
        public AdjustedProjection? Adjusted { get; private set; }
        public FantasyValue? Value { get; private set; }
        public FantasyLeague? Profile { get; private set; }
        public Guid? PublicationId { get; private set; }
        public List<CancellationToken> Tokens { get; } = [];
        public async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TransactionEntered = true;
            await action(cancellationToken);
        }
        public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SeasonProjectionPool>>([new(2026, DataSourceName.Manual, Pool.Count)]);
        public Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(int seasonEndYear, string source, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); return Task.FromResult(Pool); }
        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); return Task.FromResult(League); }
        public Task AddAsync(ObservedStats observed, BaselineProjection baseline, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); Observed = observed; Baseline = baseline; return Task.CompletedTask; }
        public Task AddDistributionAsync(Guid baselineProjectionId, ProjectionDistribution distribution, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task AddAdjustedAsync(AdjustedProjection adjusted, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); Adjusted = adjusted; return Task.CompletedTask; }
        public Task AddFantasyValueAsync(FantasyValue value, FantasyLeague league, DateTimeOffset computedAt, Guid? publicationId, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); Value = value; Profile = league; PublicationId = publicationId; return Task.CompletedTask; }
        public Task<IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>> ListForPlayerAsync(PlayerId playerId, CancellationToken cancellationToken) { Tokens.Add(cancellationToken); return Task.FromResult<IReadOnlyList<(ContextEvent, PlayerContextImpact)>>([]); }
        public Task AddAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveAgeAsync(PlayerId playerId, int seasonEndYear, string source, int age, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SeasonStatLine?> GetAsync(PlayerId playerId, int seasonEndYear, string source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ObservedStats?> GetLatestObservedAsync(PlayerId playerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BaselineProjection?> GetBaselineAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdjustedProjection?> GetAdjustedAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FantasyValue?> GetFantasyValueAsync(PlayerId playerId, Guid leagueId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task AddAsync(ContextEvent contextEvent, IReadOnlyList<PlayerContextImpact> impacts, CancellationToken cancellationToken) => throw new NotSupportedException();
        Task<ContextEvent?> IContextEventRepository.GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PlayerContextImpact?> GetImpactAsync(Guid contextEventId, PlayerId playerId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveAsync(ContextEvent contextEvent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveImpactOverrideAsync(PlayerContextImpact impact, Guid userId, DateTimeOffset overriddenAt, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
