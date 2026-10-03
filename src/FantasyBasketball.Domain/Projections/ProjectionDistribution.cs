using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

/// <summary>
/// A season projection as a distribution: per-game stat means with their covariance
/// (over <see cref="StatCovariance.Variables"/>), and games played as a Beta-Binomial.
/// Point projections stay as they were; this sits beside them.
/// </summary>
public sealed record ProjectionDistribution
{
    public ProjectionDistribution(PlayerId playerId, StatLine perGameMean, IReadOnlyList<IReadOnlyList<decimal>> covariance, BetaBinomial games, string modelVersion)
    {
        ArgumentNullException.ThrowIfNull(perGameMean);
        ArgumentNullException.ThrowIfNull(covariance);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);
        var size = StatCovariance.Variables.Count;
        if (covariance.Count != size || covariance.Any(row => row.Count != size))
        {
            throw new ArgumentException($"Covariance must be {size} × {size}.", nameof(covariance));
        }

        PlayerId = playerId;
        PerGameMean = perGameMean;
        Covariance = covariance;
        Games = games ?? throw new ArgumentNullException(nameof(games));
        ModelVersion = modelVersion;
    }

    public PlayerId PlayerId { get; }

    public StatLine PerGameMean { get; }

    public IReadOnlyList<IReadOnlyList<decimal>> Covariance { get; }

    public BetaBinomial Games { get; }

    public string ModelVersion { get; }

    /// <summary>
    /// Mean and SD of fantasy points per game under linear scoring rules. REB and PTS are
    /// folded into their parts (REB = OREB + DREB, PTS = 2·FGM + FG3M + FTM); percentage
    /// stats are not linear, so they add to the mean but not the variance.
    /// </summary>
    public (decimal Mean, decimal Sd) FantasyPointsPerGame(IEnumerable<ScoringRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var rulesList = rules.ToArray();
        var weights = EffectiveWeights(rulesList);
        var mean = rulesList.Sum(rule => PerGameMean[rule.Stat] * rule.PointsPerUnit);
        var variance = 0m;
        for (var i = 0; i < weights.Length; i++)
        {
            for (var j = 0; j < weights.Length; j++)
            {
                variance += weights[i] * Covariance[i][j] * weights[j];
            }
        }

        return (mean, (decimal)Math.Sqrt((double)Math.Max(variance, 0m)));
    }

    /// <summary>Season total = per-game points × games, independent: Var = σ²E[G²] + μ²Var G.</summary>
    public (decimal Mean, decimal Sd) SeasonFantasyPoints(IEnumerable<ScoringRule> rules)
    {
        var (mean, sd) = FantasyPointsPerGame(rules);
        var games = Games.Mean;
        var variance = (sd * sd * ((games * games) + Games.Variance)) + (mean * mean * Games.Variance);
        return (mean * games, (decimal)Math.Sqrt((double)variance));
    }

    private static decimal[] EffectiveWeights(IEnumerable<ScoringRule> rules)
    {
        var variables = StatCovariance.Variables;
        var weights = new decimal[variables.Count];
        void Add(StatKey stat, decimal points)
        {
            var index = variables.ToList().IndexOf(stat);
            if (index >= 0)
            {
                weights[index] += points;
            }
        }

        foreach (var rule in rules)
        {
            switch (rule.Stat)
            {
                case StatKey.REB:
                    Add(StatKey.OREB, rule.PointsPerUnit);
                    Add(StatKey.DREB, rule.PointsPerUnit);
                    break;
                case StatKey.PTS:
                    Add(StatKey.FGM, 2m * rule.PointsPerUnit);
                    Add(StatKey.FG3M, rule.PointsPerUnit);
                    Add(StatKey.FTM, rule.PointsPerUnit);
                    break;
                default:
                    Add(rule.Stat, rule.PointsPerUnit);
                    break;
            }
        }

        return weights;
    }
}
