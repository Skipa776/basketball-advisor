using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Landing;
using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Leagues;

public sealed class LeagueServicesTests
{
    private static readonly PlayerHeatOptions Policy = new() { RecentGames = 3, MinimumBaselineGames = 1 };
    private readonly Store store = new();

    [Fact]
    public async Task Csv_import_resolves_unique_names_reports_the_rest_and_replaces_rosters()
    {
        var jokic = store.AddPlayer("Nikola Jokić");
        store.AddPlayer("Twin Name");
        store.AddPlayer("Twin Name");
        var service = new LeagueRosterService(store, store, store);

        var result = await service.ImportCsvAsync(store.League.Id,
            "Team,Player,Mine\nMine,Nikola Jokic,yes\nMine,Twin Name,yes\nOthers,Ghost Player,\n", TestContext.Current.CancellationToken);

        result.Teams.ShouldBe(2);
        result.Players.ShouldBe(1);
        result.UnmatchedPlayers.ShouldBe(["Twin Name (line 3)", "Ghost Player (line 4)"], "ambiguous and unknown names are never guessed");
        store.Teams.Count.ShouldBe(2);
        store.Teams[0].IsUsersTeam.ShouldBeTrue();
        store.Teams[0].Players.ShouldBe([jokic.Id]);
        (await service.ListAsync(store.League.Id, TestContext.Current.CancellationToken))[0].Players.ShouldHaveSingleItem().Name.ShouldBe("Nikola Jokić");
    }

    [Theory]
    [InlineData("Team,Player\nA,X\nB,X\nC,X", "the league has 2")]
    [InlineData("Team,Player,Mine\nA,X,yes\nB,X,yes", "Mark only one team")]
    [InlineData("Name,Club\nA,X", "header with Team and Player")]
    public async Task Csv_import_rejects_rules_by_name(string csv, string reason)
    {
        var service = new LeagueRosterService(store, store, store);
        (await Should.ThrowAsync<ArgumentException>(() => service.ImportCsvAsync(store.League.Id, csv, TestContext.Current.CancellationToken)))
            .Message.ShouldContain(reason);
        store.Teams.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_league_is_not_found()
    {
        var service = new LeagueRosterService(store, store, store);
        await Should.ThrowAsync<ResourceNotFoundException>(() => service.ListAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task League_waiver_uses_league_scoring_and_leaves_out_rostered_players()
    {
        var rostered = store.AddRiser("Owned Riser");
        var free = store.AddRiser("Free Riser");
        store.Teams = [new LeagueTeam(Guid.NewGuid(), "Someone", false, [rostered.Id])];
        var waiver = Waiver();

        var result = await waiver.RisersAsync(store.League.Id, null, 10, TestContext.Current.CancellationToken);

        result.Excludes.ShouldBe("rostered");
        result.ExcludedPlayers.ShouldBe(1);
        result.Scoring.ShouldBe(store.League.Name);
        var only = result.Players.ShouldHaveSingleItem();
        only.PlayerId.ShouldBe(free.Id.Value);
        only.RecentAverage.ShouldBe(60m, "this league scores 2 per point: 30 points → 60");
    }

    [Fact]
    public async Task League_waiver_without_rosters_leaves_out_featured_stars_and_rejects_category_leagues()
    {
        store.AddRiser("Free Riser");
        var result = await Waiver().RisersAsync(store.League.Id, null, 10, TestContext.Current.CancellationToken);
        result.Excludes.ShouldBe("featured");
        result.Players.ShouldHaveSingleItem().RecentAverage.ShouldBe(60m);

        store.League = new FantasyLeague(store.League.Id, "Cats", LeagueType.Categories, 2, [], [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        await Should.ThrowAsync<ResourceConflictException>(() => Waiver().RisersAsync(store.League.Id, null, 10, TestContext.Current.CancellationToken));
    }

    private LeagueWaiverService Waiver() =>
        new(store, store, new LandingService(store, store, new PlayerHeatCalculator(new PointsScoringEngine(), Policy), Policy, new LandingOptions()));

    private sealed class Store : ILeagueRepository, ILeagueTeamRepository, IPlayerRepository, IBoxScoreRepository
    {
        private readonly List<Player> players = [];
        private readonly List<PlayerGameSample> samples = [];

        public FantasyLeague League { get; set; } = new(Guid.NewGuid(), "Double points", LeagueType.Points, 2,
            [new ScoringRule(StatKey.PTS, 2m)], [], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        public IReadOnlyList<LeagueTeam> Teams { get; set; } = [];

        public Player AddPlayer(string name)
        {
            var player = new Player(new PlayerId(Guid.NewGuid()), name, PlayerName.Normalize(name), null, ["G"], null);
            players.Add(player);
            return player;
        }

        // Three games at 10 then three at 30 points: a clear riser.
        public Player AddRiser(string name)
        {
            var player = AddPlayer(name);
            for (var day = 1; day <= 6; day++)
            {
                samples.Add(new PlayerGameSample(Guid.NewGuid(), player.Id, 2026, new DateOnly(2025, 11, day), true, true,
                    new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = day <= 3 ? 10 : 30 }),
                    new DataProvenance(DataSourceName.BasketballReference, null, DateTimeOffset.UnixEpoch, null,
                        "basketball-reference-v1", 0.95m, new string('a', 64))));
            }

            return player;
        }

        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(id == League.Id ? League : null);
        public Task<IReadOnlyList<LeagueTeam>> ListAsync(Guid leagueId, CancellationToken token) => Task.FromResult(Teams);
        public Task ReplaceAsync(Guid leagueId, IReadOnlyList<LeagueTeam> teams, CancellationToken token)
        {
            Teams = teams;
            return Task.CompletedTask;
        }

        public Task<Player?> GetAsync(PlayerId id, CancellationToken token) => Task.FromResult(players.FirstOrDefault(player => player.Id == id));
        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string name, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Player>>(players.Where(player => player.NormalizedName == name).ToArray());
        public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<BoxScorePool>>([new BoxScorePool(2026, DataSourceName.BasketballReference, 6, new DateOnly(2025, 11, 6), DateTimeOffset.UnixEpoch)]);
        public Task<IReadOnlyList<PlayerGameSample>> ListAsync(int season, string source, NbaGamePhase phase, DateOnly date, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<PlayerGameSample>>(samples.Where(sample => sample.PlayedOn <= date).ToArray());

        public Task AddAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken token) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(Player player, CancellationToken token) => throw new NotSupportedException();
        public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken token) => throw new NotSupportedException();
        public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken token) => throw new NotSupportedException();
        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken token) => throw new NotSupportedException();
        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken token) => throw new NotSupportedException();
        public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken token) => throw new NotSupportedException();
    }
}
