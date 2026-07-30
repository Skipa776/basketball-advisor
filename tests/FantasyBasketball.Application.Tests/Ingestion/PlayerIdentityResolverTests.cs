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
    public async Task N04_import_records_pending_count_and_stays_queryable()
    {
        var unique = CreatePlayer("Nikola Jokic");
        var repository = new FakePlayerRepository(
            CreatePlayer("Marcus Williams"),
            CreatePlayer("Marcus Williams"),
            unique);
        var resolver = CreateResolver(repository);
        var runs = new FakeDataImportRunRepository();
        var service = new ImportPlayersService(
            resolver,
            runs,
            new PassThroughImportTransaction(),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));

        var run = await service.ImportAsync(
            DataSourceName.BallDontLie,
            [
                CreateExternalPlayer("99", "Marcus Williams"),
                CreateExternalPlayer("15", "Nikola Jokić"),
            ],
            TestContext.Current.CancellationToken);
        var queried = await runs.GetAsync(
            run.Id,
            TestContext.Current.CancellationToken);

        run.Status.ShouldBe(DataImportRunStatus.Succeeded);
        run.RowsWritten.ShouldBe(1);
        run.PendingIdentityMatches.ShouldBe(1);
        queried.ShouldBe(run);
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

    [Fact]
    public async Task I13_failed_import_rolls_back_partial_writes_and_records_failure()
    {
        var existing = CreatePlayer("Existing Player");
        var existingIdentity = new ExternalPlayerIdentity(
            existing.Id,
            DataSourceName.BallDontLie,
            "existing",
            DateTimeOffset.UnixEpoch,
            false);
        var repository = new FakePlayerRepository(existing)
        {
            FailOnResolvedWrite = 2,
        };
        repository.Identities.Add(existingIdentity);
        var runs = new FakeDataImportRunRepository();
        var service = new ImportPlayersService(
            CreateResolver(repository),
            runs,
            new SnapshotImportTransaction(repository, runs),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var run = await service.ImportAsync(
            DataSourceName.BallDontLie,
            [
                CreateExternalPlayer("first", "First New Player"),
                CreateExternalPlayer("second", "Second New Player"),
            ],
            TestContext.Current.CancellationToken);

        run.Status.ShouldBe(DataImportRunStatus.Failed);
        repository.Players.ShouldBe([existing]);
        repository.Identities.ShouldBe([existingIdentity]);
        repository.PendingMatches.ShouldBeEmpty();
        (await runs.GetAsync(run.Id, TestContext.Current.CancellationToken)).ShouldBe(run);
    }

    [Fact]
    public async Task I06_provider_failure_returns_a_queryable_failed_run()
    {
        var repository = new FakePlayerRepository();
        var runs = new FakeDataImportRunRepository();
        var service = new ImportPlayersService(
            CreateResolver(repository),
            runs,
            new SnapshotImportTransaction(repository, runs),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var run = await service.ImportFromAsync(
            new FailingPlayerDirectoryProvider(),
            TestContext.Current.CancellationToken);

        run.Status.ShouldBe(DataImportRunStatus.Failed);
        run.FailureDetail.ShouldNotBeNull();
        run.FailureDetail.ShouldContain(nameof(HttpRequestException));
        (await runs.GetAsync(run.Id, TestContext.Current.CancellationToken)).ShouldBe(run);
        repository.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task I09_cancellation_rolls_back_partial_import()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new FakePlayerRepository
        {
            AfterResolvedWrite = cancellation.Cancel,
        };
        var runs = new FakeDataImportRunRepository();
        var service = new ImportPlayersService(
            CreateResolver(repository),
            runs,
            new SnapshotImportTransaction(repository, runs),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        await Should.ThrowAsync<OperationCanceledException>(() => service.ImportAsync(
            DataSourceName.BallDontLie,
            [
                CreateExternalPlayer("first", "First New Player"),
                CreateExternalPlayer("second", "Second New Player"),
            ],
            cancellation.Token));

        repository.Players.ShouldBeEmpty();
        repository.Identities.ShouldBeEmpty();
        repository.PendingMatches.ShouldBeEmpty();
        runs.Items.ShouldBeEmpty();
    }

    private static PlayerIdentityResolver CreateResolver(IPlayerRepository repository) =>
        new(
            repository,
            new FixedTimeProvider(
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

        public int? FailOnResolvedWrite { get; init; }

        public Action? AfterResolvedWrite { get; init; }

        private int resolvedWriteAttempts;

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
            resolvedWriteAttempts++;
            if (resolvedWriteAttempts == FailOnResolvedWrite)
            {
                throw new InvalidOperationException("Simulated provider write failure.");
            }

            if (addPlayer)
            {
                Players.Add(player);
            }

            Identities.Add(identity);
            WriteCount++;
            AfterResolvedWrite?.Invoke();
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

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FakeDataImportRunRepository : IDataImportRunRepository
    {
        public List<DataImportRun> Items { get; } = [];

        public Task AddAsync(DataImportRun run, CancellationToken cancellationToken)
        {
            Items.Add(run);
            return Task.CompletedTask;
        }

        public Task<DataImportRun?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(run => run.Id == id));
    }

    private sealed class PassThroughImportTransaction : IImportTransaction
    {
        public Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            action(cancellationToken);
    }

    private sealed class SnapshotImportTransaction(
        FakePlayerRepository players,
        FakeDataImportRunRepository runs) : IImportTransaction
    {
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken)
        {
            var playerCount = players.Players.Count;
            var identityCount = players.Identities.Count;
            var pendingCount = players.PendingMatches.Count;
            var runCount = runs.Items.Count;

            try
            {
                await action(cancellationToken);
            }
            catch
            {
                players.Players.RemoveRange(playerCount, players.Players.Count - playerCount);
                players.Identities.RemoveRange(
                    identityCount,
                    players.Identities.Count - identityCount);
                players.PendingMatches.RemoveRange(
                    pendingCount,
                    players.PendingMatches.Count - pendingCount);
                runs.Items.RemoveRange(runCount, runs.Items.Count - runCount);
                throw;
            }
        }
    }

    private sealed class FailingPlayerDirectoryProvider : IPlayerDirectoryProvider
    {
        public string Name => DataSourceName.BallDontLie;

        public DataSourceKind Kind => DataSourceKind.Api;

        public Task<IReadOnlyList<ExternalPlayer>> GetPlayersAsync(
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated 500 response.");

        public Task<IReadOnlyList<ExternalTeam>> GetTeamsAsync(
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated 500 response.");
    }
}
