using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Leagues;

public sealed class MatchupServiceTests
{
    private static readonly DateOnly Wednesday = new(2025, 11, 12);
    private readonly NbaTeamId home = new(Guid.NewGuid());
    private readonly NbaTeamId away = new(Guid.NewGuid());
    private readonly Store store = new();

    [Fact]
    public async Task Week_totals_are_scored_so_far_plus_games_left_times_recent_average()
    {
        var starter = store.AddPlayer("Starter", home);
        var rookie = store.AddPlayer("Rookie", away);
        store.Teams = [new LeagueTeam(Guid.NewGuid(), "Mine", true, [starter.Id]), new LeagueTeam(Guid.NewGuid(), "Them", false, [rookie.Id])];
        store.Game(starter.Id, new DateOnly(2025, 11, 5), 10);
        store.Game(starter.Id, new DateOnly(2025, 11, 10), 20);
        store.Scheduled(home, away, new DateOnly(2025, 11, 12));
        store.Scheduled(home, new NbaTeamId(Guid.NewGuid()), new DateOnly(2025, 11, 14));
        store.Scheduled(home, away, new DateOnly(2025, 11, 18));

        var matchup = await Service().WeekAsync(store.League.Id, null, Wednesday, TestContext.Current.CancellationToken);

        matchup.WeekStart.ShouldBe(new DateOnly(2025, 11, 10));
        matchup.WeekEnd.ShouldBe(new DateOnly(2025, 11, 16));
        var you = matchup.You.Players.ShouldHaveSingleItem();
        you.ScoredSoFar.ShouldBe(40m, "Monday's 20 points at 2 per point; last week's game is not this week");
        you.PerGame.ShouldBe(30m);
        you.GamesLeft.ShouldBe(2, "Wednesday and Friday; next Tuesday is another week");
        matchup.You.ProjectedTotal.ShouldBe(100m);
        var them = matchup.Opponent.Players.ShouldHaveSingleItem();
        them.PerGame.ShouldBeNull("no recorded games is not a zero average");
        them.GamesLeft.ShouldBe(1);
        matchup.Opponent.ProjectedTotal.ShouldBe(0m);
        matchup.Opponents.ShouldHaveSingleItem().Name.ShouldBe("Them");
    }

    [Fact]
    public async Task A_sunday_belongs_to_the_week_that_started_monday() =>
        (await WeekFor(new DateOnly(2025, 11, 16))).WeekStart.ShouldBe(new DateOnly(2025, 11, 10));

    [Fact]
    public async Task Missing_setup_is_a_conflict_that_names_the_step()
    {
        store.Teams = [new LeagueTeam(Guid.NewGuid(), "Nobody's", false, [])];
        (await Should.ThrowAsync<ResourceConflictException>(() => Service().WeekAsync(store.League.Id, null, Wednesday, TestContext.Current.CancellationToken)))
            .Message.ShouldContain("mark your team");
        store.Teams = [new LeagueTeam(Guid.NewGuid(), "Mine", true, [])];
        (await Should.ThrowAsync<ResourceConflictException>(() => Service().WeekAsync(store.League.Id, null, Wednesday, TestContext.Current.CancellationToken)))
            .Message.ShouldContain("opponent");
        store.League = new FantasyLeague(store.League.Id, "Cats", LeagueType.Categories, 2, [], [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        await Should.ThrowAsync<ResourceConflictException>(() => Service().WeekAsync(store.League.Id, null, Wednesday, TestContext.Current.CancellationToken));
    }

    private async Task<Matchup> WeekFor(DateOnly day)
    {
        store.Teams = [new LeagueTeam(Guid.NewGuid(), "Mine", true, []), new LeagueTeam(Guid.NewGuid(), "Them", false, [])];
        return await Service().WeekAsync(store.League.Id, null, day, TestContext.Current.CancellationToken);
    }

    private MatchupService Service() => new(store, store, store, store, store, new LandingOptions(), TimeProvider.System);

    private sealed class Store : ILeagueRepository, ILeagueTeamRepository, IPlayerRepository, IGameRepository, IBoxScoreRepository
    {
        private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        private readonly List<Player> players = [];
        private readonly List<PlayerGameSample> samples = [];
        private readonly List<NbaGame> schedule = [];

        public FantasyLeague League { get; set; } = new(Guid.NewGuid(), "Double points", LeagueType.Points, 4,
            [new ScoringRule(StatKey.PTS, 2m)], [], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Weekly);
        public IReadOnlyList<LeagueTeam> Teams { get; set; } = [];

        public Player AddPlayer(string name, NbaTeamId team)
        {
            var player = new Player(new PlayerId(Guid.NewGuid()), name, PlayerName.Normalize(name), team, ["G"], null);
            players.Add(player);
            return player;
        }

        public void Game(PlayerId player, DateOnly day, decimal pts) =>
            samples.Add(new PlayerGameSample(Guid.NewGuid(), player, 2026, day, true, true,
                new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = pts }), Provenance(DataSourceName.BasketballReference)));

        // 7:30pm US Eastern tip-off, stored in UTC like the balldontlie schedule.
        public void Scheduled(NbaTeamId homeTeam, NbaTeamId awayTeam, DateOnly day) =>
            schedule.Add(new NbaGame(Guid.NewGuid(), 2026,
                new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(19, 30)), Eastern), TimeSpan.Zero),
                homeTeam, awayTeam, null, null, "Scheduled", Provenance(DataSourceName.BallDontLie)));

        private static DataProvenance Provenance(string source) =>
            new(source, null, DateTimeOffset.UnixEpoch, null, $"{source}-v1", 1m, new string('a', 64));

        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(id == League.Id ? League : null);
        public Task<IReadOnlyList<LeagueTeam>> ListAsync(Guid leagueId, CancellationToken token) => Task.FromResult(Teams);
        public Task<Player?> GetAsync(PlayerId id, CancellationToken token) => Task.FromResult(players.FirstOrDefault(player => player.Id == id));
        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(int season, string source, NbaGamePhase phase, DateOnly date, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PlayerGameSample>>(samples.Where(sample => sample.PlayedOn <= date).ToArray());
        public Task<IReadOnlyList<NbaGame>> ListScheduledAsync(string source, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<NbaGame>>(schedule.Where(game => game.StartsAt >= fromUtc && game.StartsAt < toUtc).ToArray());

        public Task ReplaceAsync(Guid leagueId, IReadOnlyList<LeagueTeam> teams, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken token) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(Player player, CancellationToken token) => throw new NotSupportedException();
        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken token) => throw new NotSupportedException();
        public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string name, CancellationToken token) => throw new NotSupportedException();
        public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken token) => throw new NotSupportedException();
        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken token) => throw new NotSupportedException();
        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(NbaGame game, CancellationToken token) => throw new NotSupportedException();
        public Task SaveResultAsync(NbaGame latest, CancellationToken token) => throw new NotSupportedException();
        public Task<NbaGame?> GetBySourceAsync(string source, string externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<ScheduledGame>> ListFinalAsync(string source, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken token) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken token) => throw new NotSupportedException();
    }
}
