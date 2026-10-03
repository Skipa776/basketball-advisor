using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Tests.Backtest;

internal sealed class FakeBoxScoreRepository(
    IReadOnlyList<PlayerGameSample> samples) : IBoxScoreRepository
{
    public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BoxScorePool>>([]);

    public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken cancellationToken) =>
        Task.FromResult(samples.Any(sample => sample.GameId == gameId));

    public Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");

    public Task<IReadOnlyList<PlayerGameSample>> ListAsync(
        int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken) =>
        Task.FromResult(samples);
}

internal sealed class FakeSeasonStatLineRepository(
    IReadOnlyList<SeasonStatLine> lines) : ISeasonStatLineRepository
{
    public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SeasonProjectionPool>>([]);

    public Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(
        int seasonEndYear, string source, CancellationToken cancellationToken) =>
        Task.FromResult(lines);

    public Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");

    public Task SaveAgeAsync(PlayerId playerId, int seasonEndYear, string source, int age, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");

    public Task<SeasonStatLine?> GetAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        CancellationToken cancellationToken) =>
        Task.FromResult<SeasonStatLine?>(lines.FirstOrDefault(line => line.PlayerId == playerId));
}

internal sealed class FakeModelVersionRepository(params ModelVersion?[] active) : IModelVersionRepository
{
    public Task AddAsync(ModelVersion version, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");

    public Task<ModelVersion?> GetActiveAsync(string modelName, CancellationToken cancellationToken) =>
        Task.FromResult(active.FirstOrDefault(model => model?.ModelName == modelName));

    public Task ActivateAsync(string modelName, string version, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");

    public Task<IReadOnlyList<ModelVersion>> ListAsync(string modelName, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Fake store is read-only.");
}

/// <summary>Knows no players, so every position is unknown (group U).</summary>
internal sealed class FakePlayerRepository : IPlayerRepository
{
    public Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken) => Task.FromResult<Player?>(null);

    public Task AddAsync(Player player, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Player?> FindByExternalIdentityAsync(string provider, string externalId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ExternalPlayerIdentity?> FindIdentityAsync(PlayerId playerId, string provider, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task AddResolvedIdentityAsync(Player player, ExternalPlayerIdentity identity, bool addPlayer, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task AddPendingIdentityMatchAsync(PendingIdentityMatch pendingMatch, CancellationToken cancellationToken) => throw new NotSupportedException();
}
