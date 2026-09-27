using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Draft;

public sealed class DraftAssistServiceTests
{
    private readonly Store store = new();

    [Fact]
    public async Task Other_teams_draft_by_adp_until_it_is_the_users_turn()
    {
        // 4 teams, user in slot 3: picks 1 and 2 are simulated, pick 3 is the user's.
        store.Session = new DraftSession(Guid.NewGuid(), 4, 2, 3);
        var noAdp = store.Candidate("No ADP", null, 900m);
        var second = store.Candidate("Second", 2m, 50m);
        var first = store.Candidate("First", 1m, 10m);

        var made = await Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken);

        made.ShouldBe(2);
        store.Session.Picks.Select(pick => pick.PlayerId).ShouldBe([first, second], "lowest ADP first, then value for players without one");
        store.Session.IsUserPick(store.Session.CurrentPick).ShouldBeTrue();
        (await Service().SimulateToUserTurnAsync(store.Session.Id, TestContext.Current.CancellationToken)).ShouldBe(0, "never picks for the user");
        store.Session.Picks.ShouldNotContain(pick => pick.PlayerId == noAdp);
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

    private static DraftPick Pick(Guid sessionId, int number, PlayerId playerId) =>
        new(Guid.NewGuid(), sessionId, playerId, number);

    private static ExternalDraft Draft(int teams, int rounds, params (string Name, int Pick)[] picks) =>
        new("2000000000000000001", "in_progress", teams, rounds,
            picks.Select(pick => new ExternalDraftPick(pick.Pick, pick.Pick.ToString(), pick.Name)).ToArray());

    private DraftAssistService Service() => new(store, store, store, store);

    private sealed class Store : IDraftRepository, IDraftCandidateRepository, IPlayerRepository, IFantasyLeagueProvider
    {
        private readonly Guid leagueId = Guid.NewGuid();
        private readonly List<DraftCandidate> candidates = [];
        private readonly List<Player> players = [];

        public DraftSession Session { get; set; } = new(Guid.NewGuid(), 4, 2, 3);

        public ExternalDraft? LatestDraft { get; set; }

        public string Name => FantasyBasketball.Domain.Provenance.DataSourceName.Sleeper;

        public DataSourceKind Kind => DataSourceKind.Api;

        public Task<ExternalLeagueSnapshot> GetLeagueAsync(string externalLeagueId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ExternalDraft?> GetLatestDraftAsync(string externalLeagueId, CancellationToken cancellationToken) => Task.FromResult(LatestDraft);

        public PlayerId Candidate(string name, decimal? adp, decimal value)
        {
            var id = new PlayerId(Guid.NewGuid());
            players.Add(new Player(id, name, PlayerName.Normalize(name), null, ["PG"], null));
            candidates.Add(new DraftCandidate(id, value, ["PG"], adp, 0m, 0m, 0m, new Dictionary<StatKey, decimal>()));
            return id;
        }

        public Task<DraftSessionRecord?> GetSessionAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<DraftSessionRecord?>(id == Session.Id ? new DraftSessionRecord(Session, leagueId) : null);

        public Task AddPickAsync(DraftPick pick, CancellationToken cancellationToken) => Task.CompletedTask;

        Task<IReadOnlyList<DraftCandidate>> IDraftCandidateRepository.ListAsync(Guid league, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DraftCandidate>>(league == leagueId ? candidates : []);

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

        public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
