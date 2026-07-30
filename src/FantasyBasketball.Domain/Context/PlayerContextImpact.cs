using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Context;

public sealed record PlayerContextImpact
{
    private PlayerContextImpact(
        Guid id,
        Guid contextEventId,
        PlayerId playerId,
        decimal minutesDelta,
        decimal usageDelta,
        decimal assistShareDelta,
        decimal reboundShareDelta,
        decimal shotVolumeDelta,
        decimal roleRiskDelta,
        decimal projectionConfidenceDelta,
        bool isOverridden)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Context impact id cannot be empty.", nameof(id));
        }

        if (contextEventId == Guid.Empty)
        {
            throw new ArgumentException(
                "Context event id cannot be empty.",
                nameof(contextEventId));
        }

        Id = id;
        ContextEventId = contextEventId;
        PlayerId = playerId;
        MinutesDelta = minutesDelta;
        UsageDelta = usageDelta;
        AssistShareDelta = assistShareDelta;
        ReboundShareDelta = reboundShareDelta;
        ShotVolumeDelta = shotVolumeDelta;
        RoleRiskDelta = roleRiskDelta;
        ProjectionConfidenceDelta = projectionConfidenceDelta;
        IsOverridden = isOverridden;
    }

    public Guid Id { get; }

    public Guid ContextEventId { get; }

    public PlayerId PlayerId { get; }

    public decimal MinutesDelta { get; }

    public decimal UsageDelta { get; }

    public decimal AssistShareDelta { get; }

    public decimal ReboundShareDelta { get; }

    public decimal ShotVolumeDelta { get; }

    public decimal RoleRiskDelta { get; }

    public decimal ProjectionConfidenceDelta { get; }

    public bool IsOverridden { get; }

    public static PlayerContextImpact CreateDefault(
        Guid id,
        ContextEvent contextEvent,
        PlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(contextEvent);
        if (!contextEvent.AffectedPlayerIds.Contains(playerId))
        {
            throw new ArgumentException(
                "Impact player must be affected by the context event.",
                nameof(playerId));
        }

        var defaults = ContextEventCatalog.GetDefaults(contextEvent.Type);
        var scale = contextEvent.Magnitude;
        var direction = contextEvent.Direction switch
        {
            ContextDirection.Positive => 1m,
            ContextDirection.Negative => -1m,
            ContextDirection.Neutral => 0m,
            _ => throw new ArgumentOutOfRangeException(nameof(contextEvent)),
        };

        return new PlayerContextImpact(
            id,
            contextEvent.Id,
            playerId,
            Scale(defaults.MinutesDelta, defaults.MinutesUsesDirection),
            Scale(defaults.UsageDelta, defaults.UsageUsesDirection),
            Scale(defaults.AssistShareDelta, defaults.AssistUsesDirection),
            Scale(defaults.ReboundShareDelta, defaults.ReboundUsesDirection),
            0m,
            defaults.RoleRiskDelta * scale,
            0m,
            false);

        decimal Scale(decimal value, bool usesDirection) =>
            value * scale * (usesDirection ? direction : 1m);
    }

    public static PlayerContextImpact CreateOverride(
        Guid id,
        Guid contextEventId,
        PlayerId playerId,
        decimal minutesDelta,
        decimal usageDelta,
        decimal assistShareDelta,
        decimal reboundShareDelta,
        decimal shotVolumeDelta,
        decimal roleRiskDelta,
        decimal projectionConfidenceDelta) =>
        new(
            id,
            contextEventId,
            playerId,
            minutesDelta,
            usageDelta,
            assistShareDelta,
            reboundShareDelta,
            shotVolumeDelta,
            roleRiskDelta,
            projectionConfidenceDelta,
            true);

    public PlayerContextImpact Override(
        decimal minutesDelta,
        decimal usageDelta,
        decimal assistShareDelta,
        decimal reboundShareDelta,
        decimal shotVolumeDelta,
        decimal roleRiskDelta,
        decimal projectionConfidenceDelta) =>
        CreateOverride(
            Id,
            ContextEventId,
            PlayerId,
            minutesDelta,
            usageDelta,
            assistShareDelta,
            reboundShareDelta,
            shotVolumeDelta,
            roleRiskDelta,
            projectionConfidenceDelta);
}
