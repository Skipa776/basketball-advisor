using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Backtest;

public sealed class AsOfSeasonStatLineRepository(
    ISeasonStatLineRepository inner,
    DateTimeOffset asOf) : ISeasonStatLineRepository
{
    public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) =>
        inner.ListPoolsAsync(cancellationToken);

    public async Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(
        int seasonEndYear, string source, CancellationToken cancellationToken)
    {
        var rows = await inner.ListPoolAsync(seasonEndYear, source, cancellationToken);
        return AsOfGuard.Ensure(rows, asOf, AsOfGuard.KnownAt, Describe);
    }

    public async Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The backtest runner is read-only.");

    public async Task<SeasonStatLine?> GetAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        CancellationToken cancellationToken)
    {
        var row = await inner.GetAsync(playerId, seasonEndYear, source, cancellationToken);
        return row is null
            ? null
            : AsOfGuard.Ensure([row], asOf, AsOfGuard.KnownAt, Describe).Single();
    }

    private static string Describe(SeasonStatLine line) =>
        $"season stat line player {line.PlayerId.Value} season {line.SeasonEndYear} source {line.Provenance.Source}";
}
