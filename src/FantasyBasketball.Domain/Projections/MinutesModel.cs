using System.Text.Json;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>Posterior means from tools/modeling/minutes_model.py (model_params_contract).</summary>
public sealed record MinutesModelParameters(
    int AgeCenter,
    decimal MaxMinutes,
    int MinHistoryGames,
    decimal RotationFrom,
    decimal StarterFrom,
    IReadOnlyList<decimal> Weights,
    decimal Mu,
    decimal Kappa,
    IReadOnlyDictionary<string, decimal> Delta,
    decimal Beta,
    decimal Sigma,
    decimal Tau)
{
    public const string ModelName = "projection-minutes";

    public static MinutesModelParameters Parse(string json)
    {
        var parameters = JsonSerializer.Deserialize<MinutesModelParameters>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Minutes model parameters are empty.", nameof(json));
        var missing = MinutesModel.Roles.Where(role => !parameters.Delta.ContainsKey(role)).ToArray();
        return missing.Length == 0 && parameters.Weights.Count == 3
            ? parameters
            : throw new ArgumentException($"Minutes model parameters need three weights and a shift for {string.Join(", ", MinutesModel.Roles)}.", nameof(json));
    }
}

/// <summary>One prior season, <paramref name="Lag"/> seasons before the target.</summary>
public sealed record MinutesHistory(int Lag, int Games, decimal MinutesPerGame, int? Age);

/// <summary>
/// Minutes per game: games- and recency-weighted history shrunk toward the league mean for
/// short seasons, plus last season's role shift (the role prior) and a linear age term.
/// </summary>
public sealed class MinutesModel(MinutesModelParameters parameters)
{
    public static readonly IReadOnlyList<string> Roles = ["bench", "rotation", "starter", "none"];

    public decimal Project(int targetSeasonEndYear, IReadOnlyList<SeasonStatLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var history = lines
            .Select(line => new MinutesHistory(targetSeasonEndYear - line.SeasonEndYear, line.GamesPlayed, line.MinutesPerGame, line.Age))
            .Where(item => item.Lag is >= 1 and <= 3 && item.Games >= parameters.MinHistoryGames)
            .OrderBy(item => item.Lag)
            .ToArray();
        var age = history.FirstOrDefault(item => item.Age is not null) is { Age: { } years, Lag: var lag }
            ? years + lag
            : (int?)null;
        return FromHistory(age, history);
    }

    public decimal FromHistory(int? targetAge, IReadOnlyList<MinutesHistory> history)
    {
        ArgumentNullException.ThrowIfNull(history);
        var weightedGames = history.Sum(item => parameters.Weights[item.Lag - 1] * item.Games);
        var weightedMinutes = history.Sum(item => parameters.Weights[item.Lag - 1] * item.Games * item.MinutesPerGame);
        var shrunk = (weightedMinutes + (parameters.Kappa * parameters.Mu)) / (weightedGames + parameters.Kappa);
        var centeredAge = targetAge is { } years ? years - parameters.AgeCenter : 0;
        var minutes = shrunk + parameters.Delta[Role(history)] + (parameters.Beta * centeredAge);
        return Math.Clamp(minutes, 0m, parameters.MaxMinutes);
    }

    private string Role(IReadOnlyList<MinutesHistory> history) =>
        history.FirstOrDefault(item => item.Lag == 1) switch
        {
            null => "none",
            { MinutesPerGame: var mpg } when mpg >= parameters.StarterFrom => "starter",
            { MinutesPerGame: var mpg } when mpg >= parameters.RotationFrom => "rotation",
            _ => "bench",
        };
}
