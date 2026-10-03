using System.Text.Json;
using System.Text.Json.Serialization;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>Fitted by tools/modeling/projection_covariance.py (model_params_contract).</summary>
public sealed record StatCovarianceParameters(
    IReadOnlyList<StatKey> Stats,
    decimal Scale,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<decimal>>> Correlation)
{
    public const string ModelName = "projection-covariance";

    public static StatCovarianceParameters Parse(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var parameters = JsonSerializer.Deserialize<StatCovarianceParameters>(json, options)
            ?? throw new ArgumentException("Covariance parameters are empty.", nameof(json));
        return parameters.Stats.SequenceEqual(HierarchicalProjector.ModelledStats)
            && parameters.Correlation.Values.All(matrix => matrix.Count == parameters.Stats.Count
                && matrix.All(row => row.Count == parameters.Stats.Count))
            ? parameters
            : throw new ArgumentException("Covariance parameters must cover the modelled stats in order with square correlations.", nameof(json));
    }
}

/// <summary>
/// Per-game covariance of the modelled stats and minutes:
/// k² [ (m² + Var m) · D R D + r rᵀ Var m ], with Var m on the minutes diagonal and r_s Var m
/// between a stat and minutes. D holds each rate's predictive SD over the expected season
/// minutes; R is the position group's rate-residual correlation.
/// </summary>
public sealed class StatCovariance(
    StatCovarianceParameters parameters,
    ProjectionRateParameters rates,
    MinutesModelParameters minutes)
{
    /// <summary>The modelled stats, then MIN: the order of every covariance matrix.</summary>
    public static readonly IReadOnlyList<StatKey> Variables = [.. HierarchicalProjector.ModelledStats, StatKey.MIN];

    public decimal[][] PerGame(string? position, StatLine perMinute, decimal projectedMinutes, decimal expectedGames)
    {
        ArgumentNullException.ThrowIfNull(perMinute);
        var group = position is not null && rates.GroupOf.TryGetValue(position, out var known) ? known : ProjectionRateParameters.UnknownGroup;
        var correlation = parameters.Correlation.TryGetValue(group, out var matrix) ? matrix : parameters.Correlation[ProjectionRateParameters.UnknownGroup];
        var seasonMinutes = Math.Max(projectedMinutes * expectedGames, 1m);
        var stats = HierarchicalProjector.ModelledStats;
        var rate = stats.Select(stat => perMinute[stat]).ToArray();
        var sd = stats.Select((stat, index) =>
        {
            var p = rates.Stats[stat];
            var variance = (p.Phi * rate[index] / seasonMinutes) + (p.Tau * rate[index] * p.Tau * rate[index]);
            return (decimal)Math.Sqrt((double)variance);
        }).ToArray();
        var minutesVariance = (minutes.Sigma * minutes.Sigma / Math.Max(expectedGames, 1m)) + (minutes.Tau * minutes.Tau);
        var k2 = parameters.Scale * parameters.Scale;
        var n = stats.Count;
        var result = new decimal[n + 1][];
        for (var i = 0; i <= n; i++)
        {
            result[i] = new decimal[n + 1];
            for (var j = 0; j <= n; j++)
            {
                result[i][j] = k2 * (i, j) switch
                {
                    (var a, var b) when a == n && b == n => minutesVariance,
                    (var a, var b) when a == n => rate[b] * minutesVariance,
                    (var a, var b) when b == n => rate[a] * minutesVariance,
                    var (a, b) => (((projectedMinutes * projectedMinutes) + minutesVariance) * sd[a] * sd[b] * correlation[a][b])
                        + (rate[a] * rate[b] * minutesVariance),
                };
            }
        }

        return result;
    }
}
