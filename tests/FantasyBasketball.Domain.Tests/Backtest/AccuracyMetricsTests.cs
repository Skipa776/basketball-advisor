using FantasyBasketball.Domain.Backtest;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Backtest;

public sealed class AccuracyMetricsTests
{
    [Fact]
    public void B02_fixed_twenty_player_fixture_matches_hand_computed_values()
    {
        // Projected: 1..20 in input order, so rank(projected) == value and the
        // ascending sort for deciles is the identity order.
        var projected = Enumerable.Range(1, 20).Select(value => (decimal)value).ToArray();
        var actual = new[]
        {
            1m, 2m, 3m, 4m, 6m, 5m, 7m, 8m, 10m, 11m,
            9m, 12m, 13m, 19m, 15m, 16m, 17m, 18m, 14m, 20m,
        };

        // Hand computation, errors proj - actual are all zero except
        // (5,6)->-1, (6,5)->1, (9,10)->-1, (10,11)->-1, (11,9)->2,
        // (14,19)->-5, (19,14)->5.
        // MAE = (1+1+1+1+2+5+5)/20 = 16/20 = 0.8.
        // RMSE = sqrt((1+1+1+1+4+25+25)/20) = sqrt(58/20) = sqrt(2.9) = 1.7029386...

        // No ties anywhere, so Spearman rho = 1 - 6*sum(d^2)/(n*(n^2-1))
        // = 1 - 6*58/(20*399) = 1 - 348/7980 = 1 - 29/665 = 636/665 = 0.9563910...

        // Top 5 by projected: indices 15..19 (values 16..20).
        // Top 5 by actual: values 16,17,18,19,20 at indices 15,16,17,13,19.
        // Intersection {15,16,17,19} -> 4/5 = 0.8.
        // Deciles are pairs (1,2), (3,4), ..., (19,20) with means:
        // d1 (1.5,1.5) dev 0.0, d2 (3.5,3.5) 0.0, d3 (5.5,5.5) 0.0,
        // d4 (7.5,7.5) 0.0, d5 proj(9,10) act(10,11) (9.5,10.5) dev 1.0,
        // d6 proj(11,12) act(9,12) (11.5,10.5) dev -1.0,
        // d7 proj(13,14) act(13,19) (13.5,16.0) dev 2.5,
        // d8 (15.5,15.5) 0.0, d9 (17.5,17.5) 0.0,
        // d10 proj(19,20) act(14,20) (19.5,17.0) dev -2.5.
        var report = AccuracyMetrics.Compute(projected, actual, topK: 5);

        report.Count.ShouldBe(20);
        report.Mae.ShouldBe(0.8m, 0.0001m);
        report.Rmse.ShouldBe(1.7029m, 0.0001m);
        report.SpearmanRho.ShouldBe(0.9564m, 0.0001m);
        report.TopKHitRate.ShouldBe(0.8m, 0.0001m);

        report.Deciles.Count.ShouldBe(10);
        report.Deciles.Select(decile => decile.Decile).ShouldBe(Enumerable.Range(1, 10).ToArray());
        report.Deciles.ShouldAllBe(decile => decile.Count == 2);
        report.Deciles.Select(decile => decile.Deviation).ShouldBe(
            [0.0m, 0.0m, 0.0m, 0.0m, 1.0m, -1.0m, 2.5m, 0.0m, 0.0m, -2.5m], 0.0001m);
        report.Deciles[4].MeanProjected.ShouldBe(9.5m, 0.0001m);
        report.Deciles[4].MeanActual.ShouldBe(10.5m, 0.0001m);
        report.Deciles[6].MeanProjected.ShouldBe(13.5m, 0.0001m);
        report.Deciles[6].MeanActual.ShouldBe(16.0m, 0.0001m);
    }

    [Fact]
    public void B03_tied_projections_use_average_ranks()
    {
        // projected [1,2,2,3] -> ranks [1,2.5,2.5,4]; actual [1,2,3,4] -> ranks [1,2,3,4].
        // Means are 2.5 and 2.5. Covariance = (-1.5)(-1.5) + 0 + 0 + (1.5)(1.5) = 4.5.
        // Variances: 2.25+0+0+2.25 = 4.5 and 2.25+0.25+0.25+2.25 = 5.
        // rho = 4.5 / sqrt(4.5*5) = 4.5 / sqrt(22.5) = 0.9486833...
        var report = AccuracyMetrics.Compute([1m, 2m, 2m, 3m], [1m, 2m, 3m, 4m]);
        report.SpearmanRho.ShouldBe(0.9487m, 0.0001m);

        // Constant projected has zero rank variance -> rho 0.
        AccuracyMetrics.Compute([2m, 2m, 2m, 2m], [1m, 2m, 3m, 4m]).SpearmanRho.ShouldBe(0m);
    }

    [Fact]
    public void B04_identity_projection_scores_perfectly()
    {
        var values = Enumerable.Range(1, 20).Select(value => (decimal)value).ToArray();
        var report = AccuracyMetrics.Compute(values, values);
        report.Mae.ShouldBe(0m);
        report.Rmse.ShouldBe(0m);
        report.SpearmanRho.ShouldBe(1m);
        report.TopKHitRate.ShouldBe(1m);
        report.Deciles.ShouldAllBe(decile => decile.Deviation == 0m);
        report.Deciles.ShouldAllBe(decile => decile.MeanProjected == decile.MeanActual);
    }

    [Fact]
    public void B02_rejects_mismatched_lengths_and_tiny_samples()
    {
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([1m, 2m], [1m]));
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([1m], [1m, 2m]));
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([1m], [2m]));
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([], []));
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([1m, 2m], [1m, 2m], topK: 0));
        Should.Throw<ArgumentException>(() => AccuracyMetrics.Compute([1m, 2m], [1m, 2m], topK: -1));
        AccuracyMetrics.Compute([1m, 2m], [1m, 2m], topK: 1).ShouldNotBeNull();
    }

    [Fact]
    public void B11_interval_coverage_counts_actuals_inside_mean_plus_minus_z80_sd()
    {
        var coverage = IntervalCoverage.Compute([10m, 10m], [1m, 1m], [11m, 12m]);

        coverage.Coverage80.ShouldBe(0.5m);
        coverage.MeanSd.ShouldBe(1m);
        Should.Throw<ArgumentException>(() => IntervalCoverage.Compute([10m], [1m], [11m, 12m]));
    }
}
