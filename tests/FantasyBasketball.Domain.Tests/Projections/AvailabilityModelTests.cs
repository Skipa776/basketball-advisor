using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Projections;

public sealed class AvailabilityModelTests
{
    // a 0 (prior mean 0.5 at age 27, 20 mpg), no age or minutes terms, phi 2, weights 0.1 / 0.05 / 0.
    private static readonly AvailabilityModel Model = new(new AvailabilityModelParameters(27, 20m, 82, [0.1m, 0.05m, 0m], 0m, 0m, 0m, 2m));

    [Fact]
    public void AV01_beta_binomial_matches_scipy_moments_and_quantiles()
    {
        var games = new BetaBinomial(82, 2.5m, 0.8m);

        games.Mean.ShouldBe(62.1212121m, 0.00001m);
        games.Variance.ShouldBe(298.7421787m, 0.00001m);
        // scipy.stats.betabinom(82, 2.5, 0.8).ppf(0.1 / 0.5 / 0.9) = 36 / 67 / 81.
        games.Quantile(0.1m).ShouldBe(36);
        games.Quantile(0.5m).ShouldBe(67);
        games.Quantile(0.9m).ShouldBe(81);
        games.Quantile(1m).ShouldBe(82);
        games.Quantile(0m).ShouldBe(0);
    }

    [Fact]
    public void AV02_log_gamma_matches_known_values()
    {
        LogGamma.Of(0.5m).ShouldBe((decimal)Math.Log(Math.Sqrt(Math.PI)), 0.000000000001m);
        LogGamma.Of(10m).ShouldBe((decimal)Math.Log(362880), 0.000000000001m);
        LogGamma.Of(0.1m).ShouldBe(2.252712651734206m, 0.000000000001m);
    }

    [Fact]
    public void AV03_history_adds_discounted_games_played_and_missed()
    {
        var games = Model.FromHistory(27, [new AvailabilityHistory(1, 70, 82, 20m, null), new AvailabilityHistory(2, 40, 82, 25m, null)]);

        games.Alpha.ShouldBe(1m + (0.1m * 70m) + (0.05m * 40m));
        games.Beta.ShouldBe(1m + (0.1m * 12m) + (0.05m * 42m));
        Model.FromHistory(null, []).Mean.ShouldBe(41m);
    }

    [Fact]
    public void AV04_seasons_are_capped_at_their_own_length_and_old_or_empty_ones_dropped()
    {
        var lines = new[] { Line(2025, 84), Line(2021, 60), Line(2020, 40), Line(2022, 0) };

        var games = Model.Project(2026, lines, new Dictionary<int, int> { [2025] = 82, [2021] = 72 });

        // 2025: 84 capped to 82 of 82; 2021 is lag 5 and 2022 has no games: both dropped.
        games.Alpha.ShouldBe(1m + 8.2m);
        games.Beta.ShouldBe(1m);
    }

    [Fact]
    public void AV05_parameters_without_three_weights_are_rejected()
    {
        var json = """{"ageCenter":27,"mpgCenter":20,"fullSeason":82,"weights":[0.1],"a":0,"b":0,"c":0,"phi":2}""";

        Should.Throw<ArgumentException>(() => AvailabilityModelParameters.Parse(json));
    }

    private static SeasonStatLine Line(int season, int games) => new(
        new PlayerId(Guid.NewGuid()),
        season,
        games,
        20m,
        new StatLine(new Dictionary<StatKey, decimal>()),
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.MIN] = games * 20m }),
        null,
        new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)));
}
