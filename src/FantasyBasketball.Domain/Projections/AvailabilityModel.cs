using System.Text.Json;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>Posterior means from tools/modeling/availability_model.py (model_params_contract).</summary>
public sealed record AvailabilityModelParameters(
    int AgeCenter,
    decimal MpgCenter,
    int FullSeason,
    IReadOnlyList<decimal> Weights,
    decimal A,
    decimal B,
    decimal C,
    decimal Phi)
{
    public const string ModelName = "projection-availability";

    public static AvailabilityModelParameters Parse(string json)
    {
        var parameters = JsonSerializer.Deserialize<AvailabilityModelParameters>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Availability parameters are empty.", nameof(json));
        return parameters is { Weights.Count: 3, Phi: > 0m, FullSeason: > 0 }
            ? parameters
            : throw new ArgumentException("Availability parameters need three weights, a positive phi and a season length.", nameof(json));
    }
}

/// <summary>One prior season: games played out of that season's length, and its minutes per game.</summary>
public sealed record AvailabilityHistory(int Lag, int Games, int SeasonGames, decimal MinutesPerGame, int? Age);

/// <summary>
/// Games played next season ~ BetaBinomial(full season, alpha, beta): an age- and
/// minutes-dependent population prior updated by discounted games played and missed.
/// </summary>
public sealed class AvailabilityModel(AvailabilityModelParameters parameters)
{
    /// <param name="seasonGames">Each history season's length (the shortened 2019–20 and 2020–21 seasons count against their own).</param>
    public BetaBinomial Project(int targetSeasonEndYear, IReadOnlyList<SeasonStatLine> lines, IReadOnlyDictionary<int, int> seasonGames)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(seasonGames);
        var history = lines
            .Where(line => line.GamesPlayed > 0)
            .Select(line =>
            {
                var length = seasonGames.GetValueOrDefault(line.SeasonEndYear, parameters.FullSeason);
                return new AvailabilityHistory(targetSeasonEndYear - line.SeasonEndYear,
                    Math.Min(line.GamesPlayed, length), length, line.MinutesPerGame, line.Age);
            })
            .Where(item => item.Lag is >= 1 and <= 3)
            .OrderBy(item => item.Lag)
            .ToArray();
        var age = history.FirstOrDefault(item => item.Age is not null) is { Age: { } years, Lag: var lag }
            ? years + lag
            : (int?)null;
        return FromHistory(age, history);
    }

    public BetaBinomial FromHistory(int? targetAge, IReadOnlyList<AvailabilityHistory> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var centeredAge = targetAge is { } years ? years - parameters.AgeCenter : 0;
        // The newest season's minutes; with no history the centre itself (no minutes term).
        var minutes = history.Count > 0 ? (history.MinBy(item => item.Lag)!.MinutesPerGame - parameters.MpgCenter) / 10m : 0m;
        var logit = parameters.A + (parameters.B * centeredAge) + (parameters.C * minutes);
        var mean = (decimal)(1 / (1 + Math.Exp(-(double)logit)));
        var alpha = (parameters.Phi * mean) + history.Sum(item => parameters.Weights[item.Lag - 1] * item.Games);
        var beta = (parameters.Phi * (1m - mean))
            + history.Sum(item => parameters.Weights[item.Lag - 1] * (item.SeasonGames - item.Games));
        return new BetaBinomial(parameters.FullSeason, alpha, beta);
    }
}
