using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Trends;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Trends;

public sealed class PlayerPerformanceServiceTests
{
    [Fact]
    public async Task HP01_effective_policy_and_regular_season_selection_reach_the_response()
    {
        var store = new Store();
        var policy = new PlayerHeatOptions { RecentGames = 1, MinimumBaselineGames = 1, MaximumBaselineGames = 2 };
        var calculator = new PlayerHeatCalculator(new PointsScoringEngine(), policy);
        var service = new PlayerPerformanceService(store, store, calculator, policy);
        var player = new PlayerId(Guid.NewGuid());
        store.Samples = Enumerable.Range(1, 3).Select(day => new PlayerGameSample(Guid.NewGuid(), player, 2026,
            new DateOnly(2026, 1, day), true, true, new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = day * 10 }),
            new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)))).ToArray();
        var token = TestContext.Current.CancellationToken;
        (await service.ListPoolsAsync(store.League!.Id, token)).ShouldBeEmpty();
        foreach (var view in new[] { "best", "hot", "all" })
        {
            var result = await service.QueryAsync(store.League.Id, 2026, DataSourceName.Manual,
                new DateOnly(2026, 1, 3), view, 1, 10, token);
            result.Policy.ShouldBe(policy);
            result.Players.Single().PlayerId.ShouldBe(player);
            result.Players[0].CurrentAverage.ShouldBe(25m);
            result.Players[0].BaselineAverage.ShouldBe(15m);
            result.Players[0].PointsAboveBaseline.ShouldBe(15m);
            result.ObservedPlayers.ShouldBe(1);
            result.BestQualifiedPlayers.ShouldBe(1);
            result.ComparisonQualifiedPlayers.ShouldBe(1);
            result.Total.ShouldBe(1);
        }
        store.Phase.ShouldBe(NbaGamePhase.RegularSeason);
        store.Token.ShouldBe(token);
        (await service.QueryAsync(store.League.Id, 2026, DataSourceName.Manual, new DateOnly(2026, 1, 3), "best", 2, 10, token)).Players.ShouldBeEmpty();
    }

    [Fact]
    public async Task HP02_HP03_missing_league_and_invalid_mode_do_not_read_game_data()
    {
        var store = new Store();
        var policy = new PlayerHeatOptions();
        var service = new PlayerPerformanceService(store, store, new PlayerHeatCalculator(new PointsScoringEngine(), policy), policy);
        var id = store.League!.Id;
        var token = TestContext.Current.CancellationToken;
        async Task<PlayerPerformancePage> Query(string view) => await service.QueryAsync(id, 2026, DataSourceName.Manual, new DateOnly(2026, 1, 1), view, 1, 10, token);
        await Should.ThrowAsync<ArgumentException>(() => Query("unknown"));
        store.Reads.ShouldBe(0);
        var empty = await Query("all");
        empty.Players.ShouldBeEmpty();
        empty.LatestAppearance.ShouldBeNull();
        empty.LatestFetchedAt.ShouldBeNull();
        store.League = null;
        await Should.ThrowAsync<ResourceNotFoundException>(() => Query("best"));
        store.League = new FantasyLeague(id, "Category", LeagueType.Categories, 7, [], [StatKey.PTS], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        await Should.ThrowAsync<ResourceConflictException>(() => service.ListPoolsAsync(id, token));
        store.Reads.ShouldBe(1);
    }

    private sealed class Store : ILeagueRepository, IBoxScoreRepository
    {
        public FantasyLeague? League { get; set; } = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());
        public IReadOnlyList<PlayerGameSample> Samples { get; set; } = [];
        public NbaGamePhase Phase { get; private set; }
        public CancellationToken Token { get; private set; }
        public int Reads { get; private set; }
        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(League);
        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<BoxScorePool>>([]);
        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(int season, string source, NbaGamePhase phase, DateOnly date, CancellationToken token)
        {
            Reads++; Phase = phase; Token = token;
            return Task.FromResult(Samples);
        }
        public Task AddAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken token) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken token) => throw new NotSupportedException();
    }
}
