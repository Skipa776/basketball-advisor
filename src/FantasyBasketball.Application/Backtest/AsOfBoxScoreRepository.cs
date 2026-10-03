using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Backtest;

public sealed class AsOfBoxScoreRepository(
    IBoxScoreRepository inner,
    DateTimeOffset asOf) : IBoxScoreRepository
{
    public Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken cancellationToken) =>
        inner.ListPoolsAsync(cancellationToken);

    public Task<bool> ExistsAsync(Guid gameId, string source, CancellationToken cancellationToken) =>
        inner.ExistsAsync(gameId, source, cancellationToken);

    public async Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The backtest runner is read-only.");

    public async Task<IReadOnlyList<PlayerGameSample>> ListAsync(
        int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken)
    {
        var rows = await inner.ListAsync(seasonEndYear, source, phase, throughDate, cancellationToken);
        return AsOfGuard.Ensure(rows, asOf, AsOfGuard.KnownAt, Describe);
    }

    private static string Describe(PlayerGameSample sample) =>
        $"box score sample player {sample.PlayerId.Value} game {sample.GameId} played on {sample.PlayedOn:yyyy-MM-dd}";
}
