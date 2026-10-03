using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Projections;

public sealed class ProjectionDistributionTests
{
    private static readonly int Size = StatCovariance.Variables.Count;

    [Fact]
    public void SC01_minutes_variance_correlates_every_stat_through_minutes()
    {
        // No rate noise (phi = tau = 0): only minutes vary, so Cov(x_s, x_t) = r_s r_t Var m.
        var covariance = new StatCovariance(Identity(scale: 1m), Rates(phi: 0m, tau: 0m), Minutes(sigma: 0m, tau: 2m));
        var perMinute = new StatLine(HierarchicalProjector.ModelledStats.ToDictionary(stat => stat, _ => 0.1m));

        var sigma = covariance.PerGame("PG", perMinute, 30m, 60m);

        sigma[0][1].ShouldBe(0.1m * 0.1m * 4m);
        sigma[Size - 1][Size - 1].ShouldBe(4m);
        sigma[2][Size - 1].ShouldBe(0.1m * 4m);
    }

    [Fact]
    public void SC02_points_and_rebounds_fold_into_their_parts()
    {
        // Only FGM varies (variance 1). PTS 1 + FGM 2 gives FGM an effective weight 2·1 + 2 = 4.
        var fgm = HierarchicalProjector.ModelledStats.ToList().IndexOf(StatKey.FGM);
        var distribution = Distribution(Diagonal(fgm, 1m));

        var (mean, sd) = distribution.FantasyPointsPerGame([new ScoringRule(StatKey.PTS, 1m), new ScoringRule(StatKey.FGM, 2m), new ScoringRule(StatKey.REB, 1m)]);

        sd.ShouldBe(4m);
        mean.ShouldBe(20m + 16m + 10m);
    }

    [Fact]
    public void SC03_season_total_combines_per_game_and_games_variance()
    {
        var fgm = HierarchicalProjector.ModelledStats.ToList().IndexOf(StatKey.FGM);
        var distribution = Distribution(Diagonal(fgm, 1m));
        var rules = new[] { new ScoringRule(StatKey.FGM, 1m) };
        var games = distribution.Games;

        var (mean, sd) = distribution.SeasonFantasyPoints(rules);

        mean.ShouldBe(8m * games.Mean);
        sd.ShouldBe((decimal)Math.Sqrt((double)((games.Mean * games.Mean) + games.Variance + (64m * games.Variance))), 0.0001m);
    }

    [Fact]
    public void SC04_covariance_parameters_out_of_order_or_not_square_are_rejected()
    {
        static string Stats(IEnumerable<StatKey> stats) => "[" + string.Join(",", stats.Select(stat => "\"" + stat + "\"")) + "]";
        var reversed = "{\"stats\":" + Stats(HierarchicalProjector.ModelledStats.Reverse()) + ",\"scale\":1,\"correlation\":{}}";
        var ragged = "{\"stats\":" + Stats(HierarchicalProjector.ModelledStats) + ",\"scale\":1,\"correlation\":{\"U\":[[1]]}}";

        Should.Throw<ArgumentException>(() => StatCovarianceParameters.Parse(reversed));
        Should.Throw<ArgumentException>(() => StatCovarianceParameters.Parse(ragged));
    }

    private static ProjectionDistribution Distribution(IReadOnlyList<IReadOnlyList<decimal>> covariance) => new(
        new PlayerId(Guid.NewGuid()),
        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = 20m, [StatKey.FGM] = 8m, [StatKey.REB] = 10m }),
        covariance,
        new BetaBinomial(82, 6m, 2m),
        "test");

    private static decimal[][] Diagonal(int index, decimal variance) =>
        Enumerable.Range(0, Size).Select(i => Enumerable.Range(0, Size).Select(j => i == index && j == index ? variance : 0m).ToArray()).ToArray();

    private static StatCovarianceParameters Identity(decimal scale) => new(
        HierarchicalProjector.ModelledStats,
        scale,
        new Dictionary<string, IReadOnlyList<IReadOnlyList<decimal>>>
        {
            ["U"] = Enumerable.Range(0, Size - 1).Select(i => (IReadOnlyList<decimal>)Enumerable.Range(0, Size - 1).Select(j => i == j ? 1m : 0m).ToArray()).ToArray(),
        });

    private static ProjectionRateParameters Rates(decimal phi, decimal tau) => new(
        27, 100m, new Dictionary<string, string>(),
        HierarchicalProjector.ModelledStats.ToDictionary(stat => stat, _ => new StatRateParameters(
            [1m, 0.5m, 0.25m], 100m, new Dictionary<string, decimal> { ["U"] = 0.1m }, 0m, 0m, phi, tau)));

    private static MinutesModelParameters Minutes(decimal sigma, decimal tau) => new(
        27, 42m, 5, 18m, 28m, [1m, 0.5m, 0.25m], 20m, 10m,
        new Dictionary<string, decimal> { ["bench"] = 0m, ["rotation"] = 0m, ["starter"] = 0m, ["none"] = 0m }, 0m, sigma, tau);
}
