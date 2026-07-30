using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Ingestion;

public sealed class PlayerIdentityResolverTests
{
    public static TheoryData<string, string> NormalizationCases =>
        new()
        {
            { "Nikola Jokić", "nikola jokic" },
            { "De'Aaron Fox", "deaaron fox" },
            { "Karl-Anthony Towns", "karlanthony towns" },
            { "P.J. Tucker", "pj tucker" },
            { "Jaren Jackson Jr.", "jaren jackson" },
            { "  Luka   Dončić ", "luka doncic" },
        };

    [Theory]
    [MemberData(nameof(NormalizationCases))]
    public void N01_normalization_matches_the_contract(string input, string expected)
    {
        PlayerName.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public async Task N02_known_provider_identity_resolves_without_writing()
    {
        var player = CreatePlayer("Nikola Jokic");
        var repository = new FakePlayerRepository(player);
        repository.Identities.Add(new ExternalPlayerIdentity(
            player.Id,
            DataSourceName.BallDontLie,
            "42",
            DateTimeOffset.UnixEpoch,
            false));
        var resolver = CreateResolver(repository);

        var resolution = await resolver.ResolveAsync(
            CreateExternalPlayer("42", "Someone Else"),
            TestContext.Current.CancellationToken);

        resolution.Player.ShouldBe(player);
        resolution.PendingMatch.ShouldBeNull();
        resolution.CreatedPlayer.ShouldBeFalse();
        repository.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task N03_ambiguous_name_creates_pending_match_and_no_link()
    {
        var repository = new FakePlayerRepository(
            CreatePlayer("Marcus Williams"),
            CreatePlayer("Marcus Williams"));
        var resolver = CreateResolver(repository);

        var resolution = await resolver.ResolveAsync(
            CreateExternalPlayer("99", "Marcus Williams"),
            TestContext.Current.CancellationToken);

        resolution.Player.ShouldBeNull();
        resolution.PendingMatch.ShouldNotBeNull();
        resolution.PendingMatch.CandidatePlayerIds.Count.ShouldBe(2);
        repository.Identities.ShouldBeEmpty();
        repository.PendingMatches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task N04_ambiguous_player_does_not_block_later_resolutions()
    {
        var unique = CreatePlayer("Nikola Jokic");
        var repository = new FakePlayerRepository(
            CreatePlayer("Marcus Williams"),
            CreatePlayer("Marcus Williams"),
            unique);
        var resolver = CreateResolver(repository);

        var ambiguous = await resolver.ResolveAsync(
            CreateExternalPlayer("99", "Marcus Williams"),
            TestContext.Current.CancellationToken);
        var resolved = await resolver.ResolveAsync(
            CreateExternalPlayer("15", "Nikola Jokić"),
            TestContext.Current.CancellationToken);

        ambiguous.PendingMatch.ShouldNotBeNull();
        resolved.Player.ShouldBe(unique);
        repository.PendingMatches.Count.ShouldBe(1);
        repository.Identities.Count.ShouldBe(1);
    }

    [Fact]
    public async Task N05_conflicting_external_id_preserves_link_and_creates_pending_match()
    {
        var player = CreatePlayer("Nikola Jokic");
        var existing = new ExternalPlayerIdentity(
            player.Id,
            DataSourceName.BallDontLie,
            "old-id",
            DateTimeOffset.UnixEpoch,
            false);
        var repository = new FakePlayerRepository(player);
        repository.Identities.Add(existing);
        var resolver = CreateResolver(repository);

        var resolution = await resolver.ResolveAsync(
            CreateExternalPlayer("new-id", "Nikola Jokic"),
            TestContext.Current.CancellationToken);

        resolution.Player.ShouldBeNull();
        resolution.PendingMatch.ShouldNotBeNull();
        repository.Identities.ShouldBe([existing]);
        repository.PendingMatches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Matching_ladder_uses_team_then_birth_date_and_creates_when_absent()
    {
        var firstTeam = new NbaTeamId(Guid.NewGuid());
        var secondTeam = new NbaTeamId(Guid.NewGuid());
        var birthDate = new DateOnly(1990, 1, 2);
        var teamMatch = CreatePlayer("Same Name", firstTeam, new DateOnly(1991, 2, 3));
        var birthDateMatch = CreatePlayer("Same Name", secondTeam, birthDate);
        var repository = new FakePlayerRepository(teamMatch, birthDateMatch);
        var resolver = CreateResolver(repository);

        var byTeam = await resolver.ResolveAsync(
            CreateExternalPlayer("team", "Same Name", firstTeam),
            TestContext.Current.CancellationToken);
        var byBirthDate = await resolver.ResolveAsync(
            CreateExternalPlayer("birth", "Same Name", null, birthDate),
            TestContext.Current.CancellationToken);
        var created = await resolver.ResolveAsync(
            CreateExternalPlayer("new", "New Player"),
            TestContext.Current.CancellationToken);

        byTeam.Player.ShouldBe(teamMatch);
        byBirthDate.Player.ShouldBe(birthDateMatch);
        created.Player.ShouldNotBeNull();
        created.CreatedPlayer.ShouldBeTrue();
        repository.Identities.Count.ShouldBe(3);
    }

    private static PlayerIdentityResolver CreateResolver(IPlayerRepository repository) =>
        new(
            repository,
            new FakeTimeProvider(
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));

    private static ExternalPlayer CreateExternalPlayer(
        string externalId,
        string fullName,
        NbaTeamId? teamId = null,
        DateOnly? birthDate = null) =>
        new(
            externalId,
            fullName,
            teamId,
            ["C"],
            birthDate,
            new DataProvenance(
                DataSourceName.BallDontLie,
                externalId,
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero),
                null,
                "balldontlie-v1",
                DataSourceConfidence.OfficialApi,
                new string('a', 64)));

    private static Player CreatePlayer(
        string fullName,
        NbaTeamId? teamId = null,
        DateOnly? birthDate = null) =>
        new(
            new PlayerId(Guid.NewGuid()),
            fullName,
            PlayerName.Normalize(fullName),
            teamId,
            ["C"],
            birthDate);

    private sealed class FakePlayerRepository(params Player[] players) : IPlayerRepository
    {
        public List<Player> Players { get; } = [.. players];

        public List<ExternalPlayerIdentity> Identities { get; } = [];

        public List<PendingIdentityMatch> PendingMatches { get; } = [];

        public int WriteCount { get; private set; }

        public Task AddAsync(Player player, CancellationToken cancellationToken)
        {
            Players.Add(player);
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken) =>
            Task.FromResult(Players.SingleOrDefault(player => player.Id == id));

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            var identity = Identities.SingleOrDefault(value =>
                value.Provider == provider && value.ExternalId == externalId);
            return Task.FromResult(identity is null
                ? null
                : Players.Single(player => player.Id == identity.PlayerId));
        }

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>(
                Players.Where(player => player.NormalizedName == normalizedName).ToArray());

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            Task.FromResult(Identities.SingleOrDefault(value =>
                value.PlayerId == playerId && value.Provider == provider));

        public Task AddResolvedIdentityAsync(
            Player player,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken)
        {
            if (addPlayer)
            {
                Players.Add(player);
            }

            Identities.Add(identity);
            WriteCount++;
            return Task.CompletedTask;
        }

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken)
        {
            PendingMatches.Add(pendingMatch);
            WriteCount++;
            return Task.CompletedTask;
        }
    }
}
