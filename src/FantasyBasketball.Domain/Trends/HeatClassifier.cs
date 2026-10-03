using System.Text.Json;
using FantasyBasketball.Domain.Statistics;

namespace FantasyBasketball.Domain.Trends;

public sealed record MinutesCv(decimal MinutesFrom, decimal MinutesTo, decimal Cv);

/// <summary>Fitted offline by tools/modeling/heat_prior.py; stored as model "heat-prior".</summary>
public sealed record HeatPriorParameters(
    int RecentGames,
    int MinBaselineGames,
    int MaxBaselineGames,
    decimal MinBaselineMinutes,
    decimal Nu,
    decimal TauRelative,
    decimal EffectFloorPoints,
    decimal EffectFloorSd,
    IReadOnlyList<MinutesCv> CvByMinutes)
{
    public const string ModelName = "heat-prior";

    public static HeatPriorParameters Parse(string json) =>
        JsonSerializer.Deserialize<HeatPriorParameters>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new ArgumentException("Heat prior parameters are empty.", nameof(json));
}

public sealed record HeatPosterior(decimal ShiftMean, decimal ShiftSd, decimal EffectFloor, decimal PHot, decimal PCold);

public enum HeatLabel
{
    None,
    Hot,
    Cold,
}

public enum HeatCause
{
    Role,
    Shooting,
    Mixed,
}

/// <summary>One played, final appearance scored under the league's rules.</summary>
public sealed record HeatAppearance(
    DateOnly PlayedOn,
    decimal FantasyPoints,
    decimal Minutes,
    decimal FieldGoalAttempts,
    decimal FreeThrowAttempts,
    decimal Turnovers);

public sealed record HeatAssessment(
    HeatLabel Label,
    HeatCause? Cause,
    decimal Probability,
    HeatPosterior Posterior,
    ProductionDecomposition? Decomposition,
    int BaselineGames,
    DateOnly ThroughDate);

/// <summary>
/// Descriptive HOT/COLD labels: the last few appearances against the disjoint 10–30 before
/// them, with an empirical-Bayes shrunk shift. A label enters at P ≥ 0.75 and leaves below
/// 0.60, replayed through the season so it does not flicker between games.
/// </summary>
public sealed class HeatClassifier(HeatPriorParameters prior)
{
    public const decimal EnterProbability = 0.75m;
    public const decimal ExitProbability = 0.60m;
    public const decimal RoleShare = 0.60m;
    public const decimal ShootingShare = 0.30m;

    public HeatPosterior Posterior(IReadOnlyList<decimal> baseline, decimal baselineMinutes, IReadOnlyList<decimal> recent)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(recent);
        var meanBaseline = baseline.Average();
        var variance = baseline.Sum(value => (value - meanBaseline) * (value - meanBaseline)) / (baseline.Count - 1);
        var scale = Math.Abs(meanBaseline);
        var poolSd = Cv(baselineMinutes) * scale;
        var shrunk = (((baseline.Count - 1) * variance) + (prior.Nu * poolSd * poolSd)) / (baseline.Count - 1 + prior.Nu);
        var se2 = shrunk * ((1m / recent.Count) + (1m / baseline.Count));
        var tau = prior.TauRelative * scale;
        var tau2 = tau * tau;
        var shift = recent.Average() - meanBaseline;
        var mean = tau2 / (tau2 + se2) * shift;
        var sd = Sqrt(tau2 * se2 / (tau2 + se2));
        var floor = Math.Max(prior.EffectFloorPoints, prior.EffectFloorSd * Sqrt(shrunk));
        return new HeatPosterior(
            mean,
            sd,
            floor,
            1m - NormalDistribution.Cdf((floor - mean) / sd),
            NormalDistribution.Cdf((-floor - mean) / sd));
    }

    /// <summary>The label after the last appearance, or null when the player does not qualify then.</summary>
    public HeatAssessment? Classify(IReadOnlyList<HeatAppearance> season)
    {
        ArgumentNullException.ThrowIfNull(season);
        var label = HeatLabel.None;
        HeatAssessment? latest = null;
        for (var end = prior.RecentGames + prior.MinBaselineGames; end <= season.Count; end++)
        {
            var recent = season.Skip(end - prior.RecentGames).Take(prior.RecentGames).ToArray();
            var baselineStart = Math.Max(0, end - prior.RecentGames - prior.MaxBaselineGames);
            var baseline = season.Skip(baselineStart).Take(end - prior.RecentGames - baselineStart).ToArray();
            latest = Assess(baseline, recent, ref label);
        }

        return latest;
    }

    private HeatAssessment? Assess(HeatAppearance[] baseline, HeatAppearance[] recent, ref HeatLabel label)
    {
        var minutes = baseline.Average(game => game.Minutes);
        if (minutes < prior.MinBaselineMinutes || baseline.Average(game => game.FantasyPoints) <= 0m)
        {
            label = HeatLabel.None;
            return null;
        }

        var posterior = Posterior(
            baseline.Select(game => game.FantasyPoints).ToArray(),
            minutes,
            recent.Select(game => game.FantasyPoints).ToArray());
        label = Next(label, posterior);
        var decomposition = ProductionDecomposer.Decompose(Production(baseline), Production(recent));
        return new HeatAssessment(
            label,
            label == HeatLabel.None ? null : Cause(label, decomposition),
            label == HeatLabel.Cold ? posterior.PCold : label == HeatLabel.Hot ? posterior.PHot
                : Math.Max(posterior.PHot, posterior.PCold),
            posterior,
            decomposition,
            baseline.Length,
            recent[^1].PlayedOn);
    }

    public static HeatLabel Next(HeatLabel current, HeatPosterior posterior) => current switch
    {
        HeatLabel.Hot when posterior.PHot >= ExitProbability => HeatLabel.Hot,
        HeatLabel.Cold when posterior.PCold >= ExitProbability => HeatLabel.Cold,
        _ when posterior.PHot >= EnterProbability => HeatLabel.Hot,
        _ when posterior.PCold >= EnterProbability => HeatLabel.Cold,
        _ => HeatLabel.None,
    };

    /// <summary>Opportunity share signed by direction: a role-driven slump has share ≤ −0.60.</summary>
    private static HeatCause? Cause(HeatLabel label, ProductionDecomposition? decomposition)
    {
        if (decomposition is null)
        {
            return null;
        }

        var share = label == HeatLabel.Hot ? decomposition.OpportunityShare : -decomposition.OpportunityShare;
        return share >= RoleShare ? HeatCause.Role
            : share <= ShootingShare ? HeatCause.Shooting
            : HeatCause.Mixed;
    }

    private static WindowProduction Production(HeatAppearance[] games) =>
        new(
            games.Length,
            games.Average(game => game.Minutes),
            games.Average(game => game.FantasyPoints),
            games.Average(game => game.FieldGoalAttempts),
            games.Average(game => game.FreeThrowAttempts),
            games.Average(game => game.Turnovers));

    private decimal Cv(decimal minutes) =>
        (prior.CvByMinutes.FirstOrDefault(bin => bin.MinutesFrom <= minutes && minutes < bin.MinutesTo)
            ?? prior.CvByMinutes[^1]).Cv;

    private static decimal Sqrt(decimal value) => (decimal)Math.Sqrt((double)value);
}
