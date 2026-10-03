using System.Text.Json;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Projections;

namespace FantasyBasketball.Application.Tests.Backtest;

/// <summary>
/// Hand-set versions of the four M2 models: rates with kappa 0 and unit weights (last season's
/// rates), minutes with a +3 starter shift, availability and an identity covariance.
/// </summary>
internal static class ModelFixtures
{
    private static readonly DateTimeOffset FittedAt = new(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public static ModelVersion Rates(IReadOnlyList<int>? trainSeasons = null) => Version(
        ProjectionRateParameters.ModelName, "rates-test", trainSeasons, new
        {
            ageCenter = 27,
            minHistoryMinutes = 100m,
            groupOf = new { },
            stats = HierarchicalProjector.ModelledStats.ToDictionary(
                stat => stat.ToString(),
                _ => new { weights = new[] { 1m, 1m, 1m }, kappa = 0m, mu = new { G = 0m, W = 0m, B = 0m, U = 0m }, alpha = 0m, beta = 0m, phi = 1m, tau = 0.1m }),
        });

    public static ModelVersion Minutes(IReadOnlyList<int>? trainSeasons = null) => Version(
        MinutesModelParameters.ModelName, "minutes-test", trainSeasons, new
        {
            ageCenter = 27,
            maxMinutes = 42m,
            minHistoryGames = 5,
            rotationFrom = 18m,
            starterFrom = 28m,
            weights = new[] { 1m, 1m, 1m },
            mu = 20m,
            kappa = 0m,
            delta = new { bench = 0m, rotation = 0m, starter = 3m, none = 0m },
            beta = 0m,
            sigma = 30m,
            tau = 1m,
        });

    public static ModelVersion Availability() => Version(
        AvailabilityModelParameters.ModelName, "availability-test", null,
        new { ageCenter = 27, mpgCenter = 20m, fullSeason = 82, weights = new[] { 0.02m, 0.01m, 0m }, a = 0.5m, b = 0m, c = 0m, phi = 2m });

    public static ModelVersion Covariance() => Version(
        StatCovarianceParameters.ModelName, "covariance-test", null, new
        {
            stats = HierarchicalProjector.ModelledStats.Select(stat => stat.ToString()),
            scale = 1m,
            correlation = new Dictionary<string, decimal[][]>
            {
                ["U"] = HierarchicalProjector.ModelledStats
                    .Select((_, i) => HierarchicalProjector.ModelledStats.Select((_, j) => i == j ? 1m : 0m).ToArray())
                    .ToArray(),
            },
        });

    public static IEnumerable<ModelVersion> All() => [Rates(), Minutes(), Availability(), Covariance()];

    private static ModelVersion Version(string name, string version, IReadOnlyList<int>? trainSeasons, object parameters) =>
        new(name, version, FittedAt, trainSeasons ?? [2025], JsonSerializer.Serialize(parameters), "{}", "test");
}
