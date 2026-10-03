using FantasyBasketball.Domain.Statistics;

namespace FantasyBasketball.Domain.Projections;

/// <summary>
/// A player's season fantasy value under one league's scoring: points per game ~ Normal(mean, sd)
/// times games played ~ Beta-Binomial, independent. What the draft simulator samples.
/// </summary>
public sealed record SeasonValueDistribution(decimal PerGameMean, decimal PerGameSd, BetaBinomial Games)
{
    public decimal Mean => PerGameMean * Games.Mean;

    public decimal Sd => (decimal)Math.Sqrt((double)(
        (PerGameSd * PerGameSd * ((Games.Mean * Games.Mean) + Games.Variance)) + (PerGameMean * PerGameMean * Games.Variance)));

    /// <summary>One season from a standard-normal draw for the per-game value and a uniform for games.</summary>
    public decimal Sample(decimal standardNormal, decimal uniform) =>
        Math.Max(0m, PerGameMean + (PerGameSd * standardNormal)) * Games.Quantile(uniform);

    /// <summary>The value at a quantile of the per-game draw, for the risk modes.</summary>
    public decimal AtQuantile(decimal z) => (PerGameMean + (PerGameSd * z)) * Games.Mean;
}
