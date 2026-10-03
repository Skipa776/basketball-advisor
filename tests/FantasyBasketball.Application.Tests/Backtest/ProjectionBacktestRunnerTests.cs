using System.Text.Json;
using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Backtest;

public sealed class ProjectionBacktestRunnerTests
{
    private static readonly DateTimeOffset AsOf = new(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BR01_runner_scores_model_and_naive_against_actuals()
    {
        // Points-only lines keep ESPN scoring to points: naive = last season's PTS per game.
        // A: naive 10, actual 12 -> error 2. B: naive 20, actual 17 -> error 3. MAE 2.5.
        // C played 19 games, under the 20-game floor, so it is not evaluated.
        var a = Player();
        var b = Player();
        var c = Player();
        var runner = Runner(
            [Line(a, 10m), Line(b, 20m), Line(c, 15m)],
            [.. Games(a, 20, 12m), .. Games(b, 20, 17m), .. Games(c, 19, 30m)]);

        var result = await runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken);

        result.PlayerCount.ShouldBe(2);
        result.TrainSeasonEndYear.ShouldBe(2025);
        result.Naive.Mae.ShouldBe(2.5m);
        result.Model.Count.ShouldBe(2);
        result.ModelVersion.ShouldBe(new ProjectionOptions().ModelVersion);
    }

    [Fact]
    public async Task BR02_players_without_a_training_line_are_skipped()
    {
        var a = Player();
        var b = Player();
        var rookie = Player();
        var runner = Runner(
            [Line(a, 10m), Line(b, 20m)],
            [.. Games(a, 25, 10m), .. Games(b, 25, 20m), .. Games(rookie, 40, 35m)]);

        var result = await runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken);

        result.PlayerCount.ShouldBe(2);
        result.Naive.Mae.ShouldBe(0m);
    }

    [Fact]
    public async Task BR03_empty_training_pool_throws()
    {
        var runner = Runner([], [.. Games(Player(), 20, 10m)]);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("2025");
    }

    [Fact]
    public async Task BR05_active_rate_model_is_scored_on_the_same_players()
    {
        // kappa 0, unit weights and no age term: the rates are last season's, so the
        // hierarchical projection equals "last season repeats" (MAE 2.5, as in BR-01).
        // FTM = FTA = PTS keeps ESPN scoring to points (FTM +1, FTA -1).
        var a = Player();
        var b = Player();
        var runner = Runner(
            [Line(a, 10m), Line(b, 20m)],
            [.. Games(a, 20, 12m), .. Games(b, 20, 17m)],
            ModelFixtures.Rates([2025]));

        var result = await runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken);

        result.HierarchicalVersion.ShouldBe("rates-test");
        result.Hierarchical.ShouldNotBeNull().Count.ShouldBe(2);
        result.Hierarchical.Mae.ShouldBe(result.Naive.Mae, 0.0000001m);
    }

    [Fact]
    public async Task BR06_rate_model_trained_on_the_holdout_is_leakage()
    {
        var a = Player();
        var runner = Runner([Line(a, 10m)], [.. Games(a, 20, 12m)], ModelFixtures.Rates([2025, 2026]));

        var exception = await Should.ThrowAsync<LeakageException>(() =>
            runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("rates-test");
    }

    [Fact]
    public async Task BR08_active_minutes_model_supplies_the_hierarchical_minutes()
    {
        // Both players were starters (30 mpg); a +3 starter shift projects 33 minutes, so
        // the rates (last season's) give A 11 vs actual 12 and B 22 vs 17: MAE 3.
        var a = Player();
        var b = Player();
        var runner = Runner(
            [Line(a, 10m), Line(b, 20m)],
            [.. Games(a, 20, 12m), .. Games(b, 20, 17m)],
            ModelFixtures.Rates([2025]),
            ModelFixtures.Minutes([2025]));

        var result = await runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken);

        result.MinutesVersion.ShouldBe("minutes-test");
        result.Hierarchical.ShouldNotBeNull().Mae.ShouldBe(3m, 0.0000001m);
    }

    [Fact]
    public async Task BR09_all_distribution_models_active_give_interval_coverage()
    {
        var a = Player();
        var b = Player();
        var runner = Runner(
            [Line(a, 10m), Line(b, 20m)],
            [.. Games(a, 20, 12m), .. Games(b, 20, 17m)],
            ModelFixtures.Rates([2025]),
            ModelFixtures.Minutes([2025]),
            ModelFixtures.Availability(),
            ModelFixtures.Covariance());

        var result = await runner.RunAsync(2026, DataSourceName.Manual, AsOf, TestContext.Current.CancellationToken);

        var intervals = result.Intervals.ShouldNotBeNull();
        intervals.Count.ShouldBe(2);
        intervals.MeanSd.ShouldBeGreaterThan(0m);
        result.DistributionVersions.ShouldBe("availability-test, covariance-test");
    }

    private static ProjectionBacktestRunner Runner(
        IReadOnlyList<SeasonStatLine> lines,
        IReadOnlyList<PlayerGameSample> games,
        ModelVersion? rateModel = null,
        ModelVersion? minutesModel = null,
        ModelVersion? availabilityModel = null,
        ModelVersion? covarianceModel = null)
    {
        var options = new ProjectionOptions();
        return new ProjectionBacktestRunner(
            new FakeSeasonStatLineRepository(lines),
            new FakeBoxScoreRepository(games),
            new BaselineProjector(new MinutesProjector(), options),
            options,
            new PointsScoringEngine(),
            new FakeModelVersionRepository(rateModel, minutesModel, availabilityModel, covarianceModel),
            new FakePlayerRepository());
    }

    private static PlayerId Player() => new(Guid.NewGuid());

    private static SeasonStatLine Line(PlayerId player, decimal pointsPerGame) =>
        new(
            player,
            2025,
            60,
            30m,
            new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = pointsPerGame, [StatKey.MIN] = 30m }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = pointsPerGame * 60,
                [StatKey.FTM] = pointsPerGame * 60,
                [StatKey.FTA] = pointsPerGame * 60,
                [StatKey.MIN] = 1800m,
            }),
            null,
            Provenance());

    private static IEnumerable<PlayerGameSample> Games(PlayerId player, int count, decimal points) =>
        Enumerable.Range(0, count).Select(index => new PlayerGameSample(
            Guid.NewGuid(),
            player,
            2026,
            new DateOnly(2025, 10, 21).AddDays(index),
            true,
            true,
            new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = points, [StatKey.MIN] = 30m }),
            Provenance()));

    private static DataProvenance Provenance() =>
        new(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64));
}
