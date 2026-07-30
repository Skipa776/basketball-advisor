using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using Shouldly;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;
using ExternalAdpEntry = FantasyBasketball.Application.Abstractions.AdpEntry;

namespace FantasyBasketball.Application.Tests.Ingestion;

public sealed class ImportAdpServiceTests
{
    [Fact]
    public async Task Provider_entry_resolves_identity_and_writes_canonical_adp()
    {
        var players = new FakePlayerRepository();
        var adp = new FakeAdpRepository();
        var runs = new FakeDataImportRunRepository();
        var time = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new ImportAdpService(
            new PlayerIdentityResolver(players, time),
            adp,
            runs,
            new PassThroughImportTransaction(),
            time);
        var provenance = new DataProvenance(
            DataSourceName.Manual,
            "jokic",
            DateTimeOffset.UnixEpoch,
            null,
            "manual-v1",
            DataSourceConfidence.ManualEntry,
            new string('a', 64));

        var run = await service.ImportAsync(
            DataSourceName.Manual,
            [new ExternalAdpEntry("jokic", "Nikola Jokic", 2.3m, 1.1m, provenance)],
            TestContext.Current.CancellationToken);

        run.Status.ShouldBe(DataImportRunStatus.Succeeded);
        run.RowsWritten.ShouldBe(1);
        run.PendingIdentityMatches.ShouldBe(0);
        adp.Items.Count.ShouldBe(1);
        adp.Items[0].AverageDraftPosition.ShouldBe(2.3m);
        adp.Items[0].Provenance.ShouldBe(provenance);
        (await runs.GetAsync(run.Id, TestContext.Current.CancellationToken))
            .ShouldBe(run);
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        private readonly List<Player> players = [];
        private readonly List<ExternalPlayerIdentity> identities = [];

        public Task AddAsync(Player player, CancellationToken cancellationToken)
        {
            players.Add(player);
            return Task.CompletedTask;
        }

        public Task<Player?> GetAsync(
            PlayerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(players.SingleOrDefault(player => player.Id == id));

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            var identity = identities.SingleOrDefault(candidate =>
                candidate.Provider == provider
                && candidate.ExternalId == externalId);
            return Task.FromResult(identity is null
                ? null
                : players.Single(player => player.Id == identity.PlayerId));
        }

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>(players
                .Where(player => player.NormalizedName == normalizedName)
                .ToArray());

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            Task.FromResult(identities.SingleOrDefault(identity =>
                identity.PlayerId == playerId && identity.Provider == provider));

        public Task AddResolvedIdentityAsync(
            Player player,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken)
        {
            if (addPlayer)
            {
                players.Add(player);
            }

            identities.Add(identity);
            return Task.CompletedTask;
        }

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"Unexpected pending identity for {pendingMatch.ExternalId}.");
    }

    private sealed class FakeAdpRepository : IAdpRepository
    {
        public List<CanonicalAdpEntry> Items { get; } = [];

        public Task AddAsync(
            CanonicalAdpEntry entry,
            CancellationToken cancellationToken)
        {
            Items.Add(entry);
            return Task.CompletedTask;
        }

        public Task<CanonicalAdpEntry?> GetLatestAsync(
            PlayerId playerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Items
                .Where(entry => entry.PlayerId == playerId)
                .OrderByDescending(entry => entry.Provenance.FetchedAt)
                .FirstOrDefault());
    }

    private sealed class FakeDataImportRunRepository : IDataImportRunRepository
    {
        private readonly List<DataImportRun> runs = [];

        public Task AddAsync(DataImportRun run, CancellationToken cancellationToken)
        {
            runs.Add(run);
            return Task.CompletedTask;
        }

        public Task<DataImportRun?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(runs.SingleOrDefault(run => run.Id == id));
    }

    private sealed class PassThroughImportTransaction : IImportTransaction
    {
        public Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            action(cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
