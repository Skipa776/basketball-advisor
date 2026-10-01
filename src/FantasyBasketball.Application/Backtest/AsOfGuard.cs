using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Backtest;

public static class AsOfGuard
{
    public static DateTimeOffset KnownAt(SeasonStatLine line) =>
        line.Provenance.SourceTimestamp
            ?? new DateTimeOffset(line.SeasonEndYear, 6, 30, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset KnownAt(PlayerGameSample sample) =>
        sample.Provenance.SourceTimestamp
            ?? new DateTimeOffset(
                sample.PlayedOn.ToDateTime(TimeOnly.MinValue).AddDays(1),
                TimeSpan.Zero);

    public static IReadOnlyList<T> Ensure<T>(
        IReadOnlyList<T> rows,
        DateTimeOffset asOf,
        Func<T, DateTimeOffset> knownAt,
        Func<T, string> describe)
    {
        foreach (var row in rows)
        {
            var known = knownAt(row);
            if (known > asOf)
            {
                throw new LeakageException(
                    $"Backtest leakage: {describe(row)} was known at {known:O}, after the as-of {asOf:O}.");
            }
        }

        return rows;
    }
}
