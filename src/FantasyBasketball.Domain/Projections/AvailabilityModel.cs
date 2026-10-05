using System.Text.Json;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>
/// Posterior means from tools/modeling/availability_model.py (model_params_contract). Weights and
/// prior strength are per role (rotation, starter); a fit from before roles carries one weight set
/// and one phi, read as the same for both, so older versions stay activatable.
/// </summary>
public sealed record AvailabilityModelParameters(
    int AgeCenter,
    decimal MpgCenter,
    int FullSeason,
    IReadOnlyDictionary<string, IReadOnlyList<decimal>> Weights,
    decimal A,
    decimal B,
    decimal C,
    IReadOnlyDictionary<string, decimal> Phi,
    decimal StarterFrom)
{
    public const string ModelName = "projection-availability";

    public static readonly IReadOnlyList<string> Roles = ["rotation", "starter"];

    public static AvailabilityModelParameters Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        decimal Number(string name) => root.GetProperty(name).GetDecimal();
        IReadOnlyDictionary<string, T> PerRole<T>(JsonElement element, Func<JsonElement, T> read) =>
            element.ValueKind == JsonValueKind.Object
                ? Roles.ToDictionary(role => role, role => read(element.GetProperty(role)))
                : Roles.ToDictionary(role => role, _ => read(element));
        var parameters = new AvailabilityModelParameters(
            root.GetProperty("ageCenter").GetInt32(),
            Number("mpgCenter"),
            root.GetProperty("fullSeason").GetInt32(),
            PerRole<IReadOnlyList<decimal>>(root.GetProperty("weights"), element => element.EnumerateArray().Select(value => value.GetDecimal()).ToArray()),
            Number("a"),
            Number("b"),
            Number("c"),
            PerRole(root.GetProperty("phi"), element => element.GetDecimal()),
            root.TryGetProperty("starterFrom", out var starterFrom) ? starterFrom.GetDecimal() : decimal.MaxValue);
        return parameters.FullSeason > 0 && Roles.All(role => parameters.Weights[role].Count == 3 && parameters.Phi[role] > 0m)
            ? parameters
            : throw new ArgumentException("Availability parameters need three weights and a positive phi per role, and a season length.", nameof(json));
    }
}

/// <summary>One prior season: games played out of that season's length, and its minutes per game.</summary>
public sealed record AvailabilityHistory(int Lag, int Games, int SeasonGames, decimal MinutesPerGame, int? Age);

/// <summary>
/// Games played next season ~ BetaBinomial(full season, alpha, beta): an age- and
/// minutes-dependent population prior updated by discounted games played and missed. A starter
/// (newest season at StarterFrom+ minutes) has his own prior strength and weights: his missed
/// games are mostly injuries, which repeat less than a rotation player's.
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
        var role = history.Count > 0 && history.MinBy(item => item.Lag)!.MinutesPerGame >= parameters.StarterFrom ? "starter" : "rotation";
        var weights = parameters.Weights[role];
        var phi = parameters.Phi[role];
        var alpha = (phi * mean) + history.Sum(item => weights[item.Lag - 1] * item.Games);
        var beta = (phi * (1m - mean))
            + history.Sum(item => weights[item.Lag - 1] * (item.SeasonGames - item.Games));
        return new BetaBinomial(parameters.FullSeason, alpha, beta);
    }
}
