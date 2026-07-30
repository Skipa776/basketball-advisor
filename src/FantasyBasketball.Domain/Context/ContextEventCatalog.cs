namespace FantasyBasketball.Domain.Context;

public sealed record ContextEventDefaults(
    decimal MinutesDelta,
    bool MinutesUsesDirection,
    decimal UsageDelta,
    bool UsageUsesDirection,
    decimal AssistShareDelta,
    bool AssistUsesDirection,
    decimal ReboundShareDelta,
    bool ReboundUsesDirection,
    decimal RoleRiskDelta,
    TimeSpan? DefaultExpiration);

public static class ContextEventCatalog
{
    public static ContextEventDefaults GetDefaults(ContextEventType type) =>
        type switch
        {
            ContextEventType.Trade =>
                Directional(3m, 0.15m, 0.15m, 0.05m, 0.30m, 60),
            ContextEventType.Signing =>
                Directional(2m, 0.10m, 0.05m, 0.05m, 0.20m, 60),
            ContextEventType.Injury =>
                Fixed(-999m, 0m, 0m, 0m, 0.50m, null),
            ContextEventType.ReturnFromInjury =>
                Mixed(4m, false, 0.05m, true, 0m, false, 0m, false, 0.25m, 30),
            ContextEventType.StartingLineupChange =>
                Directional(6m, 0.10m, 0.05m, 0.05m, 0.15m, 30),
            ContextEventType.BenchRoleChange =>
                Fixed(-6m, -0.10m, -0.05m, -0.05m, 0.25m, 30),
            ContextEventType.MinutesRestriction =>
                Fixed(-8m, 0m, 0m, 0m, 0.35m, 14),
            ContextEventType.CoachStatement =>
                Directional(2m, 0.05m, 0m, 0m, 0.10m, 14),
            ContextEventType.FacilitatorChange =>
                Directional(0m, 0.05m, 0.25m, 0m, 0.20m, 60),
            ContextEventType.UsageChange =>
                Directional(0m, 0.20m, 0.05m, 0m, 0.20m, 45),
            ContextEventType.PositionChange =>
                Directional(2m, 0.05m, 0.10m, 0.10m, 0.25m, 45),
            ContextEventType.RotationChange =>
                Directional(4m, 0.05m, 0m, 0m, 0.20m, 30),
            ContextEventType.RestRisk =>
                Fixed(-2m, 0m, 0m, 0m, 0.15m, 30),
            ContextEventType.DepthChartChange =>
                Directional(3m, 0.05m, 0.05m, 0.05m, 0.20m, 30),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static ContextEventDefaults Directional(
        decimal minutes,
        decimal usage,
        decimal assists,
        decimal rebounds,
        decimal roleRisk,
        int expirationDays) =>
        Mixed(
            minutes,
            true,
            usage,
            true,
            assists,
            true,
            rebounds,
            true,
            roleRisk,
            expirationDays);

    private static ContextEventDefaults Fixed(
        decimal minutes,
        decimal usage,
        decimal assists,
        decimal rebounds,
        decimal roleRisk,
        int? expirationDays) =>
        Mixed(
            minutes,
            false,
            usage,
            false,
            assists,
            false,
            rebounds,
            false,
            roleRisk,
            expirationDays);

    private static ContextEventDefaults Mixed(
        decimal minutes,
        bool minutesUsesDirection,
        decimal usage,
        bool usageUsesDirection,
        decimal assists,
        bool assistsUseDirection,
        decimal rebounds,
        bool reboundsUseDirection,
        decimal roleRisk,
        int? expirationDays) =>
        new(
            minutes,
            minutesUsesDirection,
            usage,
            usageUsesDirection,
            assists,
            assistsUseDirection,
            rebounds,
            reboundsUseDirection,
            roleRisk,
            expirationDays is { } days ? TimeSpan.FromDays(days) : null);
}
