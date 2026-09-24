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

    private DraftAssistService Service() => new(store, store, store);

    private sealed class Store : IDraftRepository, IDraftCandidateRepository, IPlayerRepository
    {
        private readonly Guid leagueId = Guid.NewGuid();
        private readonly List<DraftCandidate> candidates = [];
        private readonly List<Player> players = [];

        public DraftSession Session { get; set; } = new(Guid.NewGuid(), 4, 2, 3);

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
