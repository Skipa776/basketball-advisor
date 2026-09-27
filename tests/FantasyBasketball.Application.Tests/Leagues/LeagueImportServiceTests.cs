using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Leagues;

public sealed class LeagueImportServiceTests
{
    private readonly Store store = new();

    private static ExternalRosterPlayer Player(string id, string name, params string[] positions) =>
        new(id, name, null, positions, new DataProvenance(DataSourceName.Sleeper, id, DateTimeOffset.UnixEpoch, null, "sleeper-v1", 0.95m, new string('a', 64)));

    private static ExternalLeagueSnapshot Snapshot(int teams = 2) => new(DataSourceName.Sleeper, "1", "Fixture League", teams,
        ["PG", "SG", "UTIL", "BN"],
        new Dictionary<string, decimal> { ["pts"] = 0.5m, ["reb"] = 1m, ["dd"] = 1m, ["bonus_pt_40p"] = 2m },
        [
            new("1", "Fixture Owner", "Fixture Owner", [Player("10", "Nikola Jokić", "C"), Player("11", "Paul George", "PF", "SF", "SG")]),
            new("2", "Team 2", null, [Player("20", "Twin Name", "PG"), Player("21", "Brand New Rookie", "SG")]),
        ]);

    private LeagueImportService Service(ExternalLeagueSnapshot snapshot) => new(store, store, store, store,
        new PlayerIdentityResolver(store, TimeProvider.System), new FakeProvider(snapshot), new LeagueRosterService(store, store, store, store, store));

    [Fact]
    public void Settings_that_cannot_be_represented_are_named_and_differences_are_reported_not_applied()
    {
        var notes = LeagueImportService.Compare(store.League, Snapshot());

        notes.ShouldContain(note => note.Contains("Scoring not imported") && note.Contains("bonus_pt_40p, dd"));
        notes.ShouldContain(note => note.Contains("sleeper lineup: PG, SG, UTIL, BN") && note.Contains("not changed"));
        store.League.ScoringRules.ShouldHaveSingleItem().PointsPerUnit.ShouldBe(1m, "settings are never applied");
    }

    [Fact]
    public async Task Import_replaces_rosters_flags_your_team_and_saves_eligibility()
    {
        var jokic = store.Add("Nikola Jokić", "C");
        var george = store.Add("Paul George", "SF");
        store.Add("Twin Name", "PG");
        store.Add("Twin Name", "SG");

        var result = await Service(Snapshot()).ImportAsync(store.League.Id, "1", "1", TestContext.Current.CancellationToken);

        result.Teams.ShouldBe(2);
        result.PendingMatches.ShouldBe(1, "two canonical players share that name, so it waits for a person");
        result.Players.ShouldBe(3, "the unknown rookie becomes a new canonical player");
        store.Teams.Single(team => team.IsUsersTeam).Players.ShouldBe([jokic.Id, george.Id]);
        store.Eligibility[george.Id].ShouldBe(["PF", "SF", "SG"]);
        result.Notes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Import_refuses_a_missing_team_choice_and_too_many_teams()
    {
        await Should.ThrowAsync<ArgumentException>(() => Service(Snapshot()).ImportAsync(store.League.Id, "1", "99", TestContext.Current.CancellationToken));
        store.League = new FantasyLeague(store.League.Id, "One team", LeagueType.Points, 1, [new ScoringRule(StatKey.PTS, 1m)], [],
            [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        (await Should.ThrowAsync<ArgumentException>(() => Service(Snapshot()).ImportAsync(store.League.Id, "1", "1", TestContext.Current.CancellationToken)))
            .Message.ShouldContain("2 teams");
        store.Teams.ShouldBeEmpty();
    }

    private sealed class FakeProvider(ExternalLeagueSnapshot snapshot) : IFantasyLeagueProvider
    {
        public string Name => DataSourceName.Sleeper;
        public DataSourceKind Kind => DataSourceKind.Api;
        public Task<ExternalLeagueSnapshot> GetLeagueAsync(string externalLeagueId, CancellationToken cancellationToken) => Task.FromResult(snapshot);
        public Task<ExternalDraft?> GetLatestDraftAsync(string externalLeagueId, CancellationToken cancellationToken) => Task.FromResult<ExternalDraft?>(null);
    }

    private sealed class Store : ILeagueRepository, ILeagueTeamRepository, IPlayerRepository, ILeagueEligibilityRepository, ITeamRepository, IAvailabilityRepository
    {
        public IReadOnlyDictionary<PlayerId, PlayerAvailability> Injuries { get; set; } = new Dictionary<PlayerId, PlayerAvailability>();

        public Task ReplaceAsync(string source, IReadOnlyList<PlayerAvailability> reports, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<IReadOnlyDictionary<PlayerId, PlayerAvailability>> IAvailabilityRepository.ListAsync(CancellationToken cancellationToken) => Task.FromResult(Injuries);

        private readonly List<Player> players = [];
        private readonly List<ExternalPlayerIdentity> identities = [];

        public FantasyLeague League { get; set; } = new(Guid.NewGuid(), "Mine", LeagueType.Points, 10,
            [new ScoringRule(StatKey.PTS, 1m)], [], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);
        public IReadOnlyList<LeagueTeam> Teams { get; private set; } = [];
        public Dictionary<PlayerId, IReadOnlyList<string>> Eligibility { get; } = [];

        public Player Add(string name, string position)
        {
            var player = new Player(new PlayerId(Guid.NewGuid()), name, PlayerName.Normalize(name), null, [position], null);
            players.Add(player);
            return player;
        }

        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken token) => Task.FromResult(id == League.Id ? League : null);
        public Task<IReadOnlyList<LeagueTeam>> ListAsync(Guid leagueId, CancellationToken token) => Task.FromResult(Teams);
        public Task ReplaceAsync(Guid leagueId, IReadOnlyList<LeagueTeam> teams, CancellationToken token)
        {
            Teams = teams;
            return Task.CompletedTask;
        }

        Task<IReadOnlyDictionary<PlayerId, IReadOnlyList<string>>> ILeagueEligibilityRepository.ListAsync(Guid leagueId, CancellationToken token) =>
            Task.FromResult<IReadOnlyDictionary<PlayerId, IReadOnlyList<string>>>(Eligibility);

        public Task SaveAsync(Guid leagueId, IReadOnlyDictionary<PlayerId, IReadOnlyList<string>> positions, CancellationToken token)
        {
            foreach (var (player, eligible) in positions)
            {
                Eligibility[player] = eligible;
            }

            return Task.CompletedTask;
        }

        public Task<Player?> GetAsync(PlayerId id, CancellationToken token) => Task.FromResult(players.FirstOrDefault(player => player.Id == id));
        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string name, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<Player>>(players.Where(player => player.NormalizedName == name).ToArray());
        public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken token) =>
            Task.FromResult(identities.FirstOrDefault(identity => identity.Provider == provider && identity.ExternalId == externalId) is { } found
                ? players.First(player => player.Id == found.PlayerId) : null);
        public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken token) =>
            Task.FromResult(identities.FirstOrDefault(identity => identity.PlayerId == playerId && identity.Provider == provider));
        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken token)
        {
            if (addPlayer)
            {
                players.Add(player);
            }

            identities.Add(identity);
            return Task.CompletedTask;
        }

        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken token) => Task.CompletedTask;
        public Task<NbaTeam?> FindByAbbreviationAsync(string abbreviation, CancellationToken token) => Task.FromResult<NbaTeam?>(null);

        public Task AddAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken token) => throw new NotSupportedException();
        public Task SaveScoringAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(Player player, CancellationToken token) => throw new NotSupportedException();
        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken token) => throw new NotSupportedException();
        public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken token) => throw new NotSupportedException();
        public Task AddAsync(NbaTeam team, DataProvenance provenance, CancellationToken token) => throw new NotSupportedException();
    }
}
