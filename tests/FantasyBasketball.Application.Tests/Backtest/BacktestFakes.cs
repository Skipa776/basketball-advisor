using FantasyBasketball.Application.Abstractions;
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
