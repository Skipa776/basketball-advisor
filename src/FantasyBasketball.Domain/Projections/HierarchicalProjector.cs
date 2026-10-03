using System.Text.Json;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>Posterior means from tools/modeling/projection_model.py (model_params_contract).</summary>
public sealed record ProjectionRateParameters(
    int AgeCenter,
    decimal MinHistoryMinutes,
    IReadOnlyDictionary<string, string> GroupOf,
    IReadOnlyDictionary<StatKey, StatRateParameters> Stats)
{
    public const string ModelName = "projection-rates";

    public const string UnknownGroup = "U";

    public static ProjectionRateParameters Parse(string json)
    {
        var parameters = JsonSerializer.Deserialize<ProjectionRateParameters>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Projection rate parameters are empty.", nameof(json));
        var missing = HierarchicalProjector.ModelledStats.Where(stat => !parameters.Stats.ContainsKey(stat)).ToArray();
        return missing.Length == 0
            ? parameters
            : throw new ArgumentException($"Projection rate parameters lack {string.Join(", ", missing)}.", nameof(json));
    }
}

public sealed record StatRateParameters(
    IReadOnlyList<decimal> Weights,
    decimal Kappa,
    IReadOnlyDictionary<string, decimal> Mu,
    decimal Alpha,
    decimal Beta,
    decimal Phi,
    decimal Tau);

/// <summary>One prior season's totals, <paramref name="Lag"/> seasons before the target.</summary>
public sealed record SeasonHistory(int Lag, StatLine Totals, int? Age);

/// <summary>
/// Per-minute rates as a recency-weighted three-season history shrunk toward a
/// position-group prior, times an age term. REB and PTS are sums of modelled stats,
/// so the rebound and scoring identities hold by construction.
/// </summary>
public sealed class HierarchicalProjector(ProjectionRateParameters parameters)
{
    public static readonly IReadOnlyList<StatKey> ModelledStats =
    [
        StatKey.OREB, StatKey.DREB, StatKey.AST, StatKey.STL, StatKey.BLK, StatKey.TOV, StatKey.FGM,
        StatKey.FGA, StatKey.FG3M, StatKey.FG3A, StatKey.FTM, StatKey.FTA, StatKey.PF,
    ];

    /// <summary>
    /// Rates for <paramref name="targetSeasonEndYear"/> from whichever of the three prior
    /// seasons the player has with enough minutes; with none, the group prior alone.
    /// </summary>
    public StatLine ProjectRates(int targetSeasonEndYear, string? position, IReadOnlyList<SeasonStatLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var history = lines
            .Select(line => new SeasonHistory(targetSeasonEndYear - line.SeasonEndYear, line.Totals, line.Age))
            .Where(item => item.Lag is >= 1 and <= 3 && item.Totals[StatKey.MIN] >= parameters.MinHistoryMinutes)
            .OrderBy(item => item.Lag)
            .ToArray();
        var group = position is not null && parameters.GroupOf.TryGetValue(position, out var known)
            ? known
            : ProjectionRateParameters.UnknownGroup;
        var age = history.FirstOrDefault(item => item.Age is not null) is { Age: { } years, Lag: var lag }
            ? years + lag
            : (int?)null;
        return Rates(group, age, history);
    }

    public StatLine Rates(string group, int? targetAge, IReadOnlyList<SeasonHistory> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var rates = ModelledStats.ToDictionary(stat => stat, stat => Rate(stat, group, targetAge, history));
        rates[StatKey.REB] = rates[StatKey.OREB] + rates[StatKey.DREB];
        rates[StatKey.PTS] = (2m * rates[StatKey.FGM]) + rates[StatKey.FG3M] + rates[StatKey.FTM];
        return new StatLine(rates);
    }

    public decimal Rate(StatKey stat, string group, int? targetAge, IReadOnlyList<SeasonHistory> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var p = parameters.Stats[stat];
        var numerator = history.Sum(item => p.Weights[item.Lag - 1] * item.Totals[stat]);
        var denominator = history.Sum(item => p.Weights[item.Lag - 1] * item.Totals[StatKey.MIN]);
        var shrunk = (numerator + (p.Kappa * p.Mu[group])) / (denominator + p.Kappa);
        var centeredAge = targetAge is { } age ? age - parameters.AgeCenter : 0;
        // ponytail: exp via double, like NormalDistribution.Cdf; 1e-15 relative error is far inside the 1e-6 golden tolerance.
        return shrunk * (decimal)Math.Exp((double)(p.Alpha + (p.Beta * centeredAge)));
    }
}
