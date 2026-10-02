using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Trends;

public sealed class ProductionDecompositionTests
{
    // rolling_window_contract worked example: baseline over 20 games, window over 6.
    private static readonly WindowProduction Baseline = new(20, 24.0m, 24.0m, 12.0m, 2.5m, 1.5m);
    private static readonly WindowProduction Window = new(6, 32.0m, 36.8m, 16.0m, 4.0m, 2.0m);

    [Fact]
    public void T01_contract_worked_example_reproduces_to_four_places()
    {
        var result = ProductionDecomposer.Decompose(Baseline, Window)!;

        result.ValueChange.ShouldBe(12.8m);
        Math.Round(result.FromMinutes, 4).ShouldBe(8.6000m);
        Math.Round(result.FromUsage, 4).ShouldBe(0.4500m);
        Math.Round(result.FromEfficiency, 4).ShouldBe(3.7500m);
        Math.Round(result.OpportunityShare, 4).ShouldBe(0.7070m);
        result.SampleWeight.ShouldBe(1m);
        result.Sustainability.ShouldBe(Sustainability.OpportunityDriven);
        Math.Round(result.TrendScore!.Value, 4).ShouldBe(10.3625m);
    }

    [Fact]
    public void T02_terms_sum_to_the_value_change_for_generated_pairs()
    {
        var random = new Random(20261001);
        for (var index = 0; index < 200; index++)
        {
            var baseline = Generated(random);
            var window = Generated(random);

            var result = ProductionDecomposer.Decompose(baseline, window)!;

            Math.Abs(result.FromMinutes + result.FromUsage + result.FromEfficiency - result.ValueChange)
                .ShouldBeLessThan(0.000000001m);
        }
    }

    [Fact]
    public void T03_identical_value_change_with_opposite_causes_gets_opposite_labels()
    {
        // Both rise from 24 to 36 points per game. One plays 12 more minutes at the same rate;
        // the other plays the same minutes and converts the same usage better.
        var moreMinutes = new WindowProduction(6, 36.0m, 36.0m, 18.0m, 3.75m, 2.25m);
        var betterShooting = new WindowProduction(6, 24.0m, 36.0m, 12.0m, 2.5m, 1.5m);

        var role = ProductionDecomposer.Decompose(Baseline, moreMinutes)!;
        var shooting = ProductionDecomposer.Decompose(Baseline, betterShooting)!;

        role.ValueChange.ShouldBe(shooting.ValueChange);
        role.Sustainability.ShouldBe(Sustainability.OpportunityDriven);
        shooting.Sustainability.ShouldBe(Sustainability.EfficiencyDriven);
    }

    [Fact]
    public void T04_zero_minutes_or_zero_usage_has_no_decomposition()
    {
        ProductionDecomposer.Decompose(Baseline, Window with { MinutesPerGame = 0m }).ShouldBeNull();
        ProductionDecomposer.Decompose(
                Baseline,
                Window with { FieldGoalAttemptsPerGame = 0m, FreeThrowAttemptsPerGame = 0m, TurnoversPerGame = 0m })
            .ShouldBeNull();
    }

    [Fact]
    public void T05_constant_production_scores_zero()
    {
        var result = ProductionDecomposer.Decompose(Baseline, Baseline with { Games = 6 })!;

        result.TrendScore.ShouldBe(0m);
        result.ValueChange.ShouldBe(0m);
    }

    [Fact]
    public void T06_the_dominant_term_is_named_and_small_samples_are_unproven()
    {
        ProductionDecomposer.Decompose(Baseline, Window)!.DominantTerm.ShouldBe(DecompositionTerm.Minutes);
        var shooting = ProductionDecomposer.Decompose(
            Baseline, new WindowProduction(6, 24.0m, 36.0m, 12.0m, 2.5m, 1.5m))!;
        shooting.DominantTerm.ShouldBe(DecompositionTerm.Efficiency);

        // Two games of 24 minutes is 48 window minutes, a sample weight of 0.32.
        var thin = ProductionDecomposer.Decompose(Baseline, Window with { Games = 2, MinutesPerGame = 24m })!;
        thin.Sustainability.ShouldBe(Sustainability.Unproven);
        thin.TrendScore.ShouldBeNull();
        ProductionDecomposer.Decompose(Baseline with { Games = 9 }, Window)!.Sustainability
            .ShouldBe(Sustainability.Unproven);
    }

    private static WindowProduction Generated(Random random) =>
        new(
            random.Next(1, 30),
            Between(random, 8m, 40m),
            Between(random, 5m, 60m),
            Between(random, 2m, 25m),
            Between(random, 0m, 10m),
            Between(random, 0.5m, 5m));

    private static decimal Between(Random random, decimal low, decimal high) =>
        low + ((high - low) * random.Next(0, 10_001) / 10_000m);
}
