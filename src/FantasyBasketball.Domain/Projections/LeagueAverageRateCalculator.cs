using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

public static class LeagueAverageRateCalculator
{
    public static StatLine Calculate(
        IReadOnlyList<SeasonStatLine> pool,
        ProjectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(options);
        if (!options.IsValid())
        {
            throw new ArgumentException("Projection options are invalid.", nameof(options));
        }

        var eligible = pool
            .Where(line => line.GamesPlayed >= options.LeagueAverageMinimumGames)
            .ToArray();
        var totalMinutes = eligible.Sum(line => line.Totals[StatKey.MIN]);
        if (totalMinutes == 0m)
        {
            return new StatLine(new Dictionary<StatKey, decimal>());
        }

        return new StatLine(CountingStats()
            .ToDictionary(
                stat => stat,
                stat => eligible.Sum(line => line.Totals[stat]) / totalMinutes));
    }

    internal static IEnumerable<StatKey> CountingStats() =>
        Enum.GetValues<StatKey>().Where(stat =>
            stat is not StatKey.MIN
                and not StatKey.FG_PCT
                and not StatKey.FT_PCT
                and not StatKey.FG3_PCT);
}
