using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Trends;

public sealed class HeatClassifierTests
{
    // Close to the 2025-26 fit: nu 26, tau 0.18 of the baseline mean, CV 0.4, 2-point floor.
    private static readonly HeatClassifier Classifier = new(new HeatPriorParameters(
        5, 10, 30, 15m, 26m, 0.18m, 2m, 0m, [new MinutesCv(0m, 60m, 0.4m)]));

    [Fact]
    public void HC01_labels_enter_at_75_percent_and_leave_below_60()
    {
        HeatClassifier.Next(HeatLabel.None, Posterior(pHot: 0.74m)).ShouldBe(HeatLabel.None);
        HeatClassifier.Next(HeatLabel.None, Posterior(pHot: 0.75m)).ShouldBe(HeatLabel.Hot);
        HeatClassifier.Next(HeatLabel.Hot, Posterior(pHot: 0.60m)).ShouldBe(HeatLabel.Hot);
        HeatClassifier.Next(HeatLabel.Hot, Posterior(pHot: 0.59m)).ShouldBe(HeatLabel.None);
        HeatClassifier.Next(HeatLabel.Cold, Posterior(pCold: 0.65m)).ShouldBe(HeatLabel.Cold);
        HeatClassifier.Next(HeatLabel.Hot, Posterior(pHot: 0.10m, pCold: 0.80m)).ShouldBe(HeatLabel.Cold);
    }

    [Fact]
    public void HC02_more_minutes_at_the_same_rate_is_hot_by_role()
    {
        // 20 points in 30 minutes, then 30 points in 45 minutes with usage per minute unchanged.
        var result = Classifier.Classify([.. Games(10, 20m, 30m, 15m), .. Games(5, 30m, 45m, 22.5m)])!;

        result.Label.ShouldBe(HeatLabel.Hot);
        result.Cause.ShouldBe(HeatCause.Role);
        result.Probability.ShouldBeGreaterThanOrEqualTo(0.75m);
    }

    [Fact]
    public void HC03_better_conversion_in_the_same_role_is_hot_by_shooting()
    {
        var result = Classifier.Classify([.. Games(10, 20m, 30m, 15m), .. Games(5, 30m, 30m, 15m)])!;

        result.Label.ShouldBe(HeatLabel.Hot);
        result.Cause.ShouldBe(HeatCause.Shooting);
    }

    [Fact]
    public void HC04_fewer_minutes_is_cold_by_role()
    {
        var result = Classifier.Classify([.. Games(10, 20m, 30m, 15m), .. Games(5, 10m, 15m, 7.5m)])!;

        result.Label.ShouldBe(HeatLabel.Cold);
        result.Cause.ShouldBe(HeatCause.Role);
    }

    [Fact]
    public void HC05_short_histories_and_deep_bench_players_get_no_assessment()
    {
        Classifier.Classify([.. Games(14, 20m, 30m, 15m)]).ShouldBeNull();
        Classifier.Classify([.. Games(10, 8m, 12m, 6m), .. Games(5, 20m, 12m, 6m)]).ShouldBeNull();
    }

    [Fact]
    public void HC06_steady_production_is_not_labelled()
    {
        var result = Classifier.Classify([.. Games(25, 20m, 30m, 15m)])!;

        result.Label.ShouldBe(HeatLabel.None);
        result.Cause.ShouldBeNull();
        result.BaselineGames.ShouldBe(20);
    }

    private static HeatPosterior Posterior(decimal pHot = 0m, decimal pCold = 0m) => new(0m, 1m, 2m, pHot, pCold);

    private static IEnumerable<HeatAppearance> Games(int count, decimal points, decimal minutes, decimal fieldGoalAttempts) =>
        Enumerable.Range(0, count).Select(index => new HeatAppearance(
            new DateOnly(2025, 10, 21).AddDays(index), points, minutes, fieldGoalAttempts, 0m, 0m));
}
