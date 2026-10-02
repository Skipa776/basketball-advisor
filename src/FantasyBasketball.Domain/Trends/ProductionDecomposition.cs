namespace FantasyBasketball.Domain.Trends;

/// <summary>Per-game averages over one window of appearances.</summary>
public sealed record WindowProduction(
    int Games,
    decimal MinutesPerGame,
    decimal ValuePerGame,
    decimal FieldGoalAttemptsPerGame,
    decimal FreeThrowAttemptsPerGame,
    decimal TurnoversPerGame)
{
    public decimal TotalMinutes => MinutesPerGame * Games;

    /// <summary>Usage per minute: (FGA + 0.44 × FTA + TOV) / MIN.</summary>
    public decimal UsagePerMinute => MinutesPerGame == 0m
        ? 0m
        : (FieldGoalAttemptsPerGame + (0.44m * FreeThrowAttemptsPerGame) + TurnoversPerGame) / MinutesPerGame;
}

public enum Sustainability
{
    Unproven,
    OpportunityDriven,
    Mixed,
    EfficiencyDriven,
}

public enum DecompositionTerm
{
    Minutes,
    Usage,
    Efficiency,
}

public sealed record ProductionDecomposition(
    decimal ValueChange,
    decimal FromMinutes,
    decimal FromUsage,
    decimal FromEfficiency,
    decimal OpportunityShare,
    decimal SampleWeight,
    Sustainability Sustainability,
    DecompositionTerm DominantTerm,
    decimal? TrendScore);

/// <summary>
/// The exact three-way split of a change in fantasy value per game, V = M × Usg × Eff, by
/// symmetric (midpoint) attribution (rolling_window_contract). The terms sum to ΔV exactly:
/// FromUsage + FromEfficiency is mean minutes × ΔR, and FromMinutes is ΔM × mean R.
/// </summary>
public static class ProductionDecomposer
{
    public const int MinBaselineGames = 10;
    public const decimal FullSampleMinutes = 150m;
    public const decimal UnprovenSampleWeight = 0.35m;
    public const decimal OpportunityDrivenShare = 0.60m;
    public const decimal EfficiencyDrivenShare = 0.30m;
    public const decimal OpportunityWeight = 1.0m;
    public const decimal EfficiencyWeight = 0.35m;
    public const decimal RoleRiskWeight = 0.5m;

    /// <summary>Null when either window has no minutes or no usage: the factors are undefined.</summary>
    public static ProductionDecomposition? Decompose(
        WindowProduction baseline,
        WindowProduction window,
        decimal roleRisk = 0m)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(window);
        if (baseline.UsagePerMinute == 0m || window.UsagePerMinute == 0m)
        {
            return null;
        }

        var (m0, m1) = (baseline.MinutesPerGame, window.MinutesPerGame);
        var (u0, u1) = (baseline.UsagePerMinute, window.UsagePerMinute);
        var (r0, r1) = (baseline.ValuePerGame / m0, window.ValuePerGame / m1);
        var (e0, e1) = (r0 / u0, r1 / u1);
        var meanMinutes = (m0 + m1) / 2m;
        var fromMinutes = (m1 - m0) * (r0 + r1) / 2m;
        var fromUsage = (u1 - u0) * ((e0 + e1) / 2m) * meanMinutes;
        var fromEfficiency = (e1 - e0) * ((u0 + u1) / 2m) * meanMinutes;
        var valueChange = window.ValuePerGame - baseline.ValuePerGame;
        var share = OpportunityShare(fromMinutes, fromUsage, fromEfficiency);
        var sampleWeight = Math.Min(1m, window.TotalMinutes / FullSampleMinutes);
        var sustainability = Classify(baseline.Games, sampleWeight, share);
        decimal? score = sustainability == Sustainability.Unproven
            ? null
            : ((OpportunityWeight * (fromMinutes + fromUsage))
                + (EfficiencyWeight * fromEfficiency)
                - (RoleRiskWeight * roleRisk * Math.Abs(valueChange))) * sampleWeight;
        return new ProductionDecomposition(
            valueChange, fromMinutes, fromUsage, fromEfficiency, share, sampleWeight, sustainability,
            Dominant(fromMinutes, fromUsage, fromEfficiency), score);
    }

    private static decimal OpportunityShare(decimal fromMinutes, decimal fromUsage, decimal fromEfficiency)
    {
        var magnitude = Math.Abs(fromMinutes) + Math.Abs(fromUsage) + Math.Abs(fromEfficiency);
        return magnitude == 0m ? 0m : (fromMinutes + fromUsage) / magnitude;
    }

    private static Sustainability Classify(int baselineGames, decimal sampleWeight, decimal share) =>
        baselineGames < MinBaselineGames || sampleWeight < UnprovenSampleWeight ? Sustainability.Unproven
        : share >= OpportunityDrivenShare ? Sustainability.OpportunityDriven
        : share <= EfficiencyDrivenShare ? Sustainability.EfficiencyDriven
        : Sustainability.Mixed;

    private static DecompositionTerm Dominant(decimal fromMinutes, decimal fromUsage, decimal fromEfficiency)
    {
        var largest = Math.Max(Math.Abs(fromMinutes), Math.Max(Math.Abs(fromUsage), Math.Abs(fromEfficiency)));
        return largest == Math.Abs(fromMinutes) ? DecompositionTerm.Minutes
            : largest == Math.Abs(fromUsage) ? DecompositionTerm.Usage
            : DecompositionTerm.Efficiency;
    }
}
