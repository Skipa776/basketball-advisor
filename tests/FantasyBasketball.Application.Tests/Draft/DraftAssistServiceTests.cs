using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Draft;

public sealed class DraftAssistServiceTests
{
    private static readonly Guid SimulatedSessionId = Guid.Parse("7c6b1d4f-2a3e-4f5b-8c9d-0e1f2a3b4c5d");

    private readonly Store store = new();

    [Fact]
    public async Task Other_teams_draft_near_adp_until_it_is_the_users_turn()
    {
        // 4 teams, user in slot 3: picks 1 and 2 are simulated, pick 3 is the user's.
        store.Session = new DraftSession(SimulatedSessionId, 4, 2, 3);
        var noAdp = store.Candidate("No ADP", null, 900m);
        var second = store.Candidate("Second", 2m, 50m);
        var first = store.Candidate("First", 1m, 10m);

        var made = await Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken);

        made.ShouldBe(2);
        PlayerId[] adpPair = [first, second];
        store.Session.Picks.Select(pick => pick.PlayerId).OrderBy(value => value.Value)
            .ShouldBe(adpPair.OrderBy(value => value.Value), "both ADP players go before the no-ADP player; jitter may swap their order");
        store.Session.IsUserPick(store.Session.CurrentPick).ShouldBeTrue();
        (await Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken)).ShouldBe(0, "never picks for the user");
        store.Session.Picks.ShouldNotContain(pick => pick.PlayerId == noAdp);
    }

    [Fact]
    public async Task A_replayed_mock_makes_the_same_picks_twice()
    {
        var pool = MockPool(40);

        var first = await SimulateMockAsync(pool);
        var second = await SimulateMockAsync(pool);

        first.Select(pick => pick.PlayerId).ShouldBe(second.Select(pick => pick.PlayerId),
            "the same draft id and state always produce the same picks");
    }

    [Fact]
    public async Task Simulated_opponents_scatter_around_adp_without_reaching_past_the_band()
    {
        var pool = MockPool(40);

        var picks = await SimulateMockAsync(pool);

        var byId = pool.ToDictionary(entry => entry.Player, entry => entry.Adp);
        var opponents = picks.Where(pick => !store.Session.IsUserPick(pick.PickNumber)).ToArray();
        opponents.Length.ShouldBe(18);
        opponents.Any(pick => byId[pick.PlayerId] != pick.PickNumber)
            .ShouldBeTrue("jitter should move at least one pick off strict ADP order");
        foreach (var pick in opponents)
        {
            Math.Abs(byId[pick.PlayerId] - pick.PickNumber).ShouldBeLessThanOrEqualTo(12,
                $"pick {pick.PickNumber} reached too far from ADP");
        }
    }

    [Fact]
    public async Task A_team_with_its_starting_center_slots_filled_waits_for_a_guard_instead_of_a_third_center()
    {
        store.League = new FantasyLeague(store.LeagueId, "Need", LeagueType.Categories, 2, [], [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.PG), new RosterSlot(RosterSlotKind.C), new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);
        var firstCenter = store.Candidate("First Center", 1m, 100m, ["C"]);
        var guardA = store.Candidate("Guard A", 2m, 90m);
        var guardB = store.Candidate("Guard B", 3m, 80m);
        var secondCenter = store.Candidate("Second Center", 4m, 70m, ["C"]);
        var thirdCenter = store.Candidate("Third Center", 5m, 60m, ["C"]);
        for (var adp = 6; adp <= 15; adp++)
        {
            store.Candidate($"Guard {adp}", adp, 100m - adp);
        }

        // Snake, 2 teams: picks 1 and 4 are team 1, whose two centers have greedy-filled the C and UTIL slots.
        store.Session = new DraftSession(SimulatedSessionId, 2, 3, 2,
        [
            Pick(SimulatedSessionId, 1, firstCenter),
            Pick(SimulatedSessionId, 2, guardA),
            Pick(SimulatedSessionId, 3, guardB),
            Pick(SimulatedSessionId, 4, secondCenter),
        ]);

        var made = await Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken);

        made.ShouldBe(1, "team 1 picks at 5, then the user at 6");
        store.Session.Picks[^1].PlayerId.ShouldNotBe(thirdCenter, "every starting slot a center can fill is already taken");
        store.Session.Picks[^1].PlayerId.ShouldNotBe(firstCenter);
        store.Session.Picks[^1].PlayerId.ShouldNotBe(secondCenter);
    }

    [Fact]
    public async Task Pasted_names_are_recorded_in_order_and_stop_at_the_first_unknown()
    {
        store.Session = new DraftSession(Guid.NewGuid(), 4, 2, 3);
        var jokic = store.Candidate("Nikola Jokić", 1m, 100m);
        var luka = store.Candidate("Luka Dončić", 2m, 90m);

        var result = await Service().RecordTakenAsync(store.Session.Id, ["nikola jokic", " ", "Luka Doncic", "Nobody Real", "Luka Doncic"], TestContext.Current.CancellationToken);

        (result.Recorded, result.CurrentPick, result.StoppedAt).ShouldBe((2, 3, "Nobody Real"));
        store.Session.Picks.Select(pick => pick.PlayerId).ShouldBe([jokic, luka]);
        (await Service().RecordTakenAsync(store.Session.Id, ["Luka Doncic"], TestContext.Current.CancellationToken)).Reason.ShouldBe("Already drafted.");
    }

    [Fact]
    public async Task Simulating_without_projections_names_the_missing_step()
    {
        store.Session = new DraftSession(Guid.NewGuid(), 4, 2, 3);
        (await Should.ThrowAsync<ResourceConflictException>(() => Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken)))
            .Message.ShouldContain("projections");
    }

    [Fact]
    public async Task Syncing_mid_draft_records_only_picks_from_the_current_pick_onward()
    {
        var jokic = store.Candidate("Nikola Jokić", 1m, 100m);
        var luka = store.Candidate("Luka Dončić", 2m, 90m);
        var shai = store.Candidate("Shai Gilgeous-Alexander", 3m, 80m);
        var sessionId = Guid.NewGuid();
        store.Session = new DraftSession(sessionId, 4, 2, 3, [Pick(sessionId, 1, jokic), Pick(sessionId, 2, luka)]);
        store.LatestDraft = Draft(4, 2, ("Nikola Jokic", 1), ("Luka Doncic", 2), ("Shai Gilgeous-Alexander", 3));

        var result = await Service().SyncFromSleeperAsync(store.Session.Id, "1000000000000000001", TestContext.Current.CancellationToken);

        result.Recorded.ShouldBe(1, "picks 1 and 2 are already in the session; only pick 3 is new");
        store.Session.Picks.Select(pick => pick.PlayerId).ShouldBe([jokic, luka, shai]);
    }

    [Fact]
    public async Task Syncing_stops_at_an_unknown_name_like_the_paste_does()
    {
        store.Session = new DraftSession(Guid.NewGuid(), 4, 2, 1);
        var jokic = store.Candidate("Nikola Jokić", 1m, 100m);
        store.LatestDraft = Draft(4, 2, ("Nikola Jokic", 1), ("Nobody Real", 2), ("Luka Doncic", 3));

        var result = await Service().SyncFromSleeperAsync(store.Session.Id, "1000000000000000001", TestContext.Current.CancellationToken);

        (result.Recorded, result.StoppedAt, result.Reason).ShouldBe((1, "Nobody Real", "No player has that name."));
        store.Session.Picks.Select(pick => pick.PlayerId).ShouldBe([jokic]);
    }

    [Fact]
    public async Task Syncing_an_already_caught_up_draft_records_nothing()
    {
        var jokic = store.Candidate("Nikola Jokić", 1m, 100m);
        var luka = store.Candidate("Luka Dončić", 2m, 90m);
        var sessionId = Guid.NewGuid();
        store.Session = new DraftSession(sessionId, 4, 2, 3, [Pick(sessionId, 1, jokic), Pick(sessionId, 2, luka)]);
        store.LatestDraft = Draft(4, 2, ("Nikola Jokic", 1), ("Luka Doncic", 2));

        var result = await Service().SyncFromSleeperAsync(store.Session.Id, "1000000000000000001", TestContext.Current.CancellationToken);

        result.Recorded.ShouldBe(0);
        result.CurrentPick.ShouldBe(3);
    }

    [Fact]
    public async Task Syncing_refuses_a_draft_from_a_different_sized_league()
    {
        store.Session = new DraftSession(Guid.NewGuid(), 10, 15, 3);
        store.LatestDraft = Draft(12, 15);
        var mismatch = await Should.ThrowAsync<ResourceConflictException>(() => Service().SyncFromSleeperAsync(store.Session.Id, "1000000000000000001", TestContext.Current.CancellationToken));
        mismatch.Message.ShouldContain("12");
        mismatch.Message.ShouldContain("10");
        store.LatestDraft = null;
        (await Should.ThrowAsync<ResourceConflictException>(() => Service().SyncFromSleeperAsync(store.Session.Id, "1000000000000000001", TestContext.Current.CancellationToken)))
            .Message.ShouldContain("no draft yet");
    }

    private List<(PlayerId Player, decimal Adp)> MockPool(int count)
    {
        var pool = new List<(PlayerId, decimal)>();
        for (var adp = 1; adp <= count; adp++)
        {
            pool.Add((store.Candidate($"Mock {adp}", adp, 1000m - adp), adp));
        }

        return pool;
    }

    /// <summary>
    /// Plays a fresh 10-team, 2-round snake with fixed picks: the service simulates every
    /// opponent turn, and at the user's turns (5 and 16) the test records the lowest remaining
    /// ADP, so two runs differ only in what the simulation itself did.
    /// </summary>
    private async Task<IReadOnlyList<DraftPick>> SimulateMockAsync(List<(PlayerId Player, decimal Adp)> pool)
    {
        store.Session = new DraftSession(SimulatedSessionId, 10, 2, 5);
        var service = Service();
        while (!IsComplete(store.Session))
        {
            if (store.Session.IsUserPick(store.Session.CurrentPick))
            {
                var taken = store.Session.Picks.Select(pick => pick.PlayerId).ToHashSet();
                store.Session.MakePick(pool
                    .Where(entry => !taken.Contains(entry.Player))
                    .OrderBy(entry => entry.Adp)
                    .Select(entry => entry.Player)
                    .First());
            }
            else
            {
                await service.SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken);
            }
        }

        return store.Session.Picks;
    }

    private static bool IsComplete(DraftSession session) => session.CurrentPick > session.TeamCount * session.RoundCount;

    private static DraftPick Pick(Guid sessionId, int number, PlayerId playerId) =>
        new(Guid.NewGuid(), sessionId, playerId, number);

    private static ExternalDraft Draft(int teams, int rounds, params (string Name, int Pick)[] picks) =>
        new("2000000000000000001", "in_progress", teams, rounds,
            picks.Select(pick => new ExternalDraftPick(pick.Pick, pick.Pick.ToString(), pick.Name)).ToArray());

    private DraftAssistService Service() => new(store, store, store, store, store);

    private sealed class Store : IDraftRepository, ILeagueRepository, IDraftCandidateRepository, IPlayerRepository, IFantasyLeagueProvider
    {
        private readonly List<DraftCandidate> candidates = [];
        private readonly List<Player> players = [];

        public DraftSession Session { get; set; } = new(Guid.NewGuid(), 4, 2, 3);

        public Guid LeagueId { get; } = Guid.NewGuid();

        public FantasyLeague? League { get; set; }

        public ExternalDraft? LatestDraft { get; set; }

        public string Name => FantasyBasketball.Domain.Provenance.DataSourceName.Sleeper;

        public DataSourceKind Kind => DataSourceKind.Api;

        public Task<ExternalLeagueSnapshot> GetLeagueAsync(string externalLeagueId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExternalDraft?> GetLatestDraftAsync(string externalLeagueId, CancellationToken cancellationToken) => Task.FromResult(LatestDraft);

        public PlayerId Candidate(string name, decimal? adp, decimal value) =>
            Candidate(name, adp, value, ["PG"]);

        public PlayerId Candidate(string name, decimal? adp, decimal value, string[] positions)
        {
            var id = new PlayerId(Guid.NewGuid());
            players.Add(new Player(id, name, PlayerName.Normalize(name), null, positions, null));
            candidates.Add(new DraftCandidate(id, value, positions, adp, 0m, 0m, 0m, new Dictionary<StatKey, decimal>()));
            return id;
        }

        public Task<DraftSessionRecord?> GetSessionAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DraftSessionRecord?>(id == Session.Id ? new DraftSessionRecord(Session, LeagueId) : null);

        public Task AddPickAsync(DraftPick pick, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<FantasyLeague?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == LeagueId ? League ?? DefaultLeague() : null);

        private FantasyLeague DefaultLeague() => League = new FantasyLeague(LeagueId, "Mock", LeagueType.Categories, 10, [], [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.PG), new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily);

        public Task AddAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<FantasyLeague>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveScoringAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveSettingsAsync(FantasyLeague league, CancellationToken cancellationToken) => throw new NotSupportedException();

        Task<IReadOnlyList<DraftCandidate>> IDraftCandidateRepository.ListAsync(Guid league, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DraftCandidate>>(league == LeagueId ? candidates : []);

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>(players.Where(player => player.NormalizedName == normalizedName).ToArray());

        public Task<PagedResult<DraftSessionRecord>> ListAsync(Guid league, int page, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> HasAnyForLeagueAsync(Guid league, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddSessionAsync(DraftSession session, Guid league, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task RemoveLastPickAsync(Guid draftSessionId, int pickNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddAsync(Player player, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
