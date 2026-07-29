using System.Collections.ObjectModel;

namespace FantasyBasketball.Domain.Stats;

public sealed record StatLine
{
    private static readonly IReadOnlySet<StatKey> RatioKeys = new HashSet<StatKey>
    {
        StatKey.FG_PCT,
        StatKey.FT_PCT,
        StatKey.FG3_PCT,
    };

    public StatLine(IReadOnlyDictionary<StatKey, decimal> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Keys.Any(RatioKeys.Contains))
        {
            throw new ArgumentException(
                "Ratio stats are derived and cannot be supplied as source values.",
                nameof(values));
        }

        if (values.Any(entry => entry.Value < 0m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                "Counting stats cannot be negative.");
        }

        Values = new ReadOnlyDictionary<StatKey, decimal>(
            new Dictionary<StatKey, decimal>(values));
    }

    public IReadOnlyDictionary<StatKey, decimal> Values { get; }

    public decimal this[StatKey key] => key switch
    {
        StatKey.FG_PCT => Divide(StatKey.FGM, StatKey.FGA),
        StatKey.FT_PCT => Divide(StatKey.FTM, StatKey.FTA),
        StatKey.FG3_PCT => Divide(StatKey.FG3M, StatKey.FG3A),
        _ => Values.TryGetValue(key, out var value) ? value : 0m,
    };

    public static StatLine Aggregate(IEnumerable<StatLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var totals = new Dictionary<StatKey, decimal>();
        foreach (var line in lines)
        {
            foreach (var entry in line.Values)
            {
                totals[entry.Key] = totals.GetValueOrDefault(entry.Key) + entry.Value;
            }
        }

        return new StatLine(totals);
    }

    private decimal Divide(StatKey numerator, StatKey denominator)
    {
        var denominatorValue = this[denominator];
        return denominatorValue == 0m ? 0m : this[numerator] / denominatorValue;
    }
}
