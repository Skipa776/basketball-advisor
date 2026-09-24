using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Landing;

public sealed class LandingServiceTests
{
    private static readonly DateOnly LastDay = new(2025, 11, 6);
    private readonly Player luka = Player("Luka Dončić");
    private readonly Player riser = Player("Role Guy");
    private readonly Player flat = Player("Bench Guy");

    [Fact]
    public async Task Daily_defaults_to_the_latest_stored_day_and_scores_featured_players()
    {
        var service = Service(out var store);
        var day = await service.DailyAsync(null, TestContext.Current.CancellationToken);

        day.Date.ShouldBe(LastDay);
        store.Requested.ShouldBe((2026, DataSourceName.BasketballReference, NbaGamePhase.RegularSeason, LastDay));
        day.PoolSize.ShouldBe(3);
        var line = day.Players.ShouldHaveSingleItem();
        line.Name.ShouldBe("Luka Dončić");
        // ESPN: 30 +5 3PM +20 FGM -20 FGA +5 FTM -6 FTA +10 REB +16 AST +8 STL +4 BLK -6 TOV.
        line.FantasyPoints.ShouldBe(66m);
        // Wins PTS, REB, AST, STL, BLK, 3PM; loses TOV; ties FG% and FT% (the only shooter).
        line.CategoriesWon.ShouldBe(6);
        line.Line["PTS"].ShouldBe(30m);
        line.PlayedOn.ShouldBe(LastDay);
    }

    [Fact]
    public async Task A_featured_player_who_sat_out_shows_their_most_recent_game()
    {
        var service = Service(out var store, lukaSitsLastDay: true);
        var day = await service.DailyAsync(LastDay, TestContext.Current.CancellationToken);

        day.PoolSize.ShouldBe(2, "only the two who played on the chosen day");
        var line = day.Players.ShouldHaveSingleItem();
        line.PlayedOn.ShouldBe(LastDay.AddDays(-1));
        line.CategoriesWon.ShouldBe(6, "compared with the pool of the day he actually played");
    }

    [Fact]
    public async Task A_simulated_today_uses_only_games_before_it()
    {
        Service(out var store);
        var service = new LandingService(store, store, Calculator(), Policy, new LandingOptions { AsOf = new DateOnly(2025, 11, 5) });
        var token = TestContext.Current.CancellationToken;

        var daily = await service.DailyAsync(null, token);
        daily.Date.ShouldBe(new DateOnly(2025, 11, 4));
        daily.SimulatedToday.ShouldBe(new DateOnly(2025, 11, 5), "a replay says so");
        store.Requested!.Value.Item4.ShouldBe(new DateOnly(2025, 11, 4));
        var risers = await service.RisersAsync(null, 10, token);
        risers.ThroughDate.ShouldBe(new DateOnly(2025, 11, 4));
        risers.Players.ShouldHaveSingleItem().RecentAverage.ShouldBe((10m + 10m + 30m) / 3m, 0.0001m, "Nov 2-4: 10, 10, 30");
    }

    [Fact]
    public async Task Risers_exclude_featured_players_and_carry_streak_lift_and_status()
    {
        var service = Service(out _);
        var result = await service.RisersAsync(LastDay, 10, TestContext.Current.CancellationToken);

        result.ThroughDate.ShouldBe(LastDay);
        var top = result.Players.ShouldHaveSingleItem();
        top.Name.ShouldBe("Role Guy");
        top.RecentAverage.ShouldBe(30m);
        top.BaselineAverage.ShouldBe(10m);
        top.PercentAboveBaseline.ShouldBe(200m);
        top.Streak.ShouldBe(3);
        top.Status.ShouldBe("Must add");
        top.CategoriesWon.ShouldBe(2, "PTS above the day's mean and fewer turnovers");
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            service.RisersAsync(LastDay, 0, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0.40, 3, "Must add")]
    [InlineData(0.40, 2, "Add")]
    [InlineData(0.20, 0, "Add")]
    [InlineData(0.01, 5, "Watch")]
    [InlineData(0.00, 5, "Hold")]
    public void Riser_status_thresholds(double lift, int streak, string status) =>
        RiserStatus.For((decimal)lift, streak).ShouldBe(status);

    [Fact]
    public async Task Empty_storage_returns_no_date_and_no_players()
    {
        var store = new Store([], []);
        var service = new LandingService(store, store, Calculator(), Policy, new LandingOptions());
        (await service.DailyAsync(null, TestContext.Current.CancellationToken)).Date.ShouldBeNull();
        (await service.RisersAsync(null, 5, TestContext.Current.CancellationToken)).Players.ShouldBeEmpty();
    }

    private static readonly PlayerHeatOptions Policy = new() { RecentGames = 3, MinimumBaselineGames = 1 };

    private static PlayerHeatCalculator Calculator() => new(new PointsScoringEngine(), Policy);

    private LandingService Service(out Store store, bool lukaSitsLastDay = false)
    {
        var samples = new List<PlayerGameSample>();
        for (var day = 1; day <= 6; day++)
        {
            var date = new DateOnly(2025, 11, day);
            if (!(lukaSitsLastDay && day == 6)) samples.Add(Sample(luka, date, Stats(pts: 30, reb: 10, ast: 8, stl: 2, blk: 1, fg3m: 5, tov: 3, fgm: 10, fga: 20, ftm: 5, fta: 6)));
            samples.Add(Sample(riser, date, Stats(pts: day <= 3 ? 10 : 30)));
            samples.Add(Sample(flat, date, Stats(pts: 10)));
        }

        store = new Store(samples, [luka, riser, flat]);
        return new LandingService(store, store, Calculator(), Policy, new LandingOptions());
    }

    private static Player Player(string name) =>
        new(new PlayerId(Guid.NewGuid()), name, PlayerName.Normalize(name), null, ["G"], null);

    private static StatLine Stats(decimal pts = 0, decimal reb = 0, decimal ast = 0, decimal stl = 0, decimal blk = 0,
        decimal fg3m = 0, decimal tov = 0, decimal fgm = 0, decimal fga = 0, decimal ftm = 0, decimal fta = 0) =>
        new(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = pts,
            [StatKey.REB] = reb,
            [StatKey.AST] = ast,
            [StatKey.STL] = stl,
            [StatKey.BLK] = blk,
            [StatKey.FG3M] = fg3m,
            [StatKey.TOV] = tov,
            [StatKey.FGM] = fgm,
            [StatKey.FGA] = fga,
            [StatKey.FTM] = ftm,
            [StatKey.FTA] = fta,
        });

    private static PlayerGameSample Sample(Player player, DateOnly date, StatLine line) =>
        new(Guid.NewGuid(), player.Id, 2026, date, true, true, line,
            new DataProvenance(DataSourceName.BasketballReference, null, DateTimeOffset.UnixEpoch, null,
                "basketball-reference-v1", 0.95m, new string('a', 64)));

    private sealed class Store(IReadOnlyList<PlayerGameSample> samples, IReadOnlyList<Player> players)
        : IBoxScoreRepository, IPlayerRepository
    {
        public (int, string, NbaGamePhase, DateOnly)? Requested { get; private set; }
        public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken) => throw new NotSupportedException();


        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<BoxScorePool>>(samples.Count == 0 ? [] :
                [new BoxScorePool(2026, DataSourceName.BasketballReference, 18, LastDay, DateTimeOffset.UnixEpoch)]);

        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(int season, string source, NbaGamePhase phase, DateOnly date, CancellationToken token)
        {
            Requested = (season, source, phase, date);
            return Task.FromResult<IReadOnlyList<PlayerGameSample>>(samples.Where(sample => sample.PlayedOn <= date).ToArray());
        }

        public Task<Player?> GetAsync(PlayerId id, CancellationToken token) =>
            Task.FromResult(players.FirstOrDefault(player => player.Id == id));

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string name, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Player>>(players.Where(player => player.NormalizedName == name).ToArray());

        public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(Player player, CancellationToken token) => throw new NotSupportedException();
        public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken token) => throw new NotSupportedException();
        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken token) => throw new NotSupportedException();
        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken token) => throw new NotSupportedException();
    }
}
