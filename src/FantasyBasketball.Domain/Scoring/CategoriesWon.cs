using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Scoring;

/// <summary>
/// How many standard categories one stat line beats the average of a pool in.
/// Counting stats compare with the pool's mean; FG% and FT% compare with the
/// pool's combined makes over attempts, and zero attempts never wins; turnovers
/// win when lower. Ties win nothing.
/// </summary>
public static class CategoriesWon
{
    public static int Count(StatLine line, IReadOnlyList<StatLine> pool)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(pool);
        if (pool.Count == 0)
        {
            throw new ArgumentException("A comparison pool needs at least one line.", nameof(pool));
        }

        return LeagueCatalog.StandardCategories.Count(category => Wins(category, line, pool));
    }

    private static bool Wins(StatKey category, StatLine line, IReadOnlyList<StatLine> pool) => category switch
    {
        StatKey.FG_PCT => BeatsRatio(line, pool, StatKey.FGM, StatKey.FGA),
        StatKey.FT_PCT => BeatsRatio(line, pool, StatKey.FTM, StatKey.FTA),
        StatKey.TOV => line[StatKey.TOV] < pool.Average(entry => entry[StatKey.TOV]),
        _ => line[category] > pool.Average(entry => entry[category]),
    };

    private static bool BeatsRatio(StatLine line, IReadOnlyList<StatLine> pool, StatKey made, StatKey attempted)
    {
        var attempts = pool.Sum(entry => entry[attempted]);
        return line[attempted] > 0m
            && attempts > 0m
            && line[made] / line[attempted] > pool.Sum(entry => entry[made]) / attempts;
    }
}
