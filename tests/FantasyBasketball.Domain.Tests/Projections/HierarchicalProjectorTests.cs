using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Projections;

public sealed class HierarchicalProjectorTests
{
    private static readonly PlayerId Player = new(Guid.NewGuid());

    [Fact]
    public void HP01_no_usable_history_falls_back_to_the_group_prior_with_its_age_term()
    {
        var projector = new HierarchicalProjector(Parameters(beta: -0.01m));

        var rates = projector.ProjectRates(2026, position: null, lines: []);

        // Unknown position reads group U; no age known means the age term is exp(0).
        rates[StatKey.AST].ShouldBe(0.2m);
        projector.Rate(StatKey.AST, "G", 37, []).ShouldBe(0.1m * (decimal)Math.Exp(-0.1), 0.0000001m);
    }

    [Fact]
    public void HP02_rebounds_and_points_are_sums_of_modelled_rates()
    {
        var rates = new HierarchicalProjector(Parameters()).ProjectRates(2026, "C", [Line(2025, 2000m, 300m, null)]);

        rates[StatKey.REB].ShouldBe(rates[StatKey.OREB] + rates[StatKey.DREB]);
        rates[StatKey.PTS].ShouldBe((2m * rates[StatKey.FGM]) + rates[StatKey.FG3M] + rates[StatKey.FTM]);
    }

    [Fact]
    public void HP03_history_is_recency_weighted_and_drops_old_or_short_seasons()
    {
        var projector = new HierarchicalProjector(Parameters());
        var lines = new[]
        {
            Line(2025, 1000m, 300m, 25), // lag 1, weight 1
            Line(2024, 1000m, 100m, 24), // lag 2, weight 0.5
            Line(2023, 50m, 50m, 23), // under 100 minutes: dropped
            Line(2022, 1000m, 900m, 22), // lag 4: dropped
        };

        var rate = projector.ProjectRates(2026, "PG", lines)[StatKey.AST];

        // (300 + 0.5*100 + kappa 100 * mu 0.1) / (1000 + 0.5*1000 + 100); age 26 is centred to -1 with beta 0.
        rate.ShouldBe(360m / 1600m);
    }

    [Fact]
    public void HP04_parameters_missing_a_modelled_stat_are_rejected()
    {
        var json = """{"ageCenter":27,"minHistoryMinutes":100,"groupOf":{},"stats":{}}""";

        Should.Throw<ArgumentException>(() => ProjectionRateParameters.Parse(json)).Message.ShouldContain("OREB");
    }

    private static ProjectionRateParameters Parameters(decimal beta = 0m) => new(
        27,
        100m,
        new Dictionary<string, string> { ["PG"] = "G", ["SG"] = "G", ["SF"] = "W", ["PF"] = "B", ["C"] = "B" },
        HierarchicalProjector.ModelledStats.ToDictionary(
            stat => stat,
            _ => new StatRateParameters(
                [1m, 0.5m, 0.25m],
                100m,
                new Dictionary<string, decimal> { ["G"] = 0.1m, ["W"] = 0.1m, ["B"] = 0.1m, ["U"] = 0.2m },
                0m,
                beta,
                1m,
                0.1m)));

    private static SeasonStatLine Line(int season, decimal minutes, decimal assists, int? age) => new(
        Player,
        season,
        60,
        minutes / 60m,
        new StatLine(new Dictionary<StatKey, decimal>()),
        new StatLine(HierarchicalProjector.ModelledStats.ToDictionary(stat => stat, _ => assists / 2m)
            .Concat([KeyValuePair.Create(StatKey.MIN, minutes)])
            .ToDictionary(pair => pair.Key, pair => pair.Key == StatKey.AST ? assists : pair.Value)),
        null,
        new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)),
        age);
}
