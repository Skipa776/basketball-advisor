using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Context;

public sealed record ContextImpactOverride(
    decimal MinutesDelta,
    decimal UsageDelta,
    decimal AssistShareDelta,
    decimal ReboundShareDelta,
    decimal ShotVolumeDelta,
    decimal RoleRiskDelta,
    decimal ProjectionConfidenceDelta);

public sealed class ContextEventService(
    IContextEventRepository repository,
    TimeProvider timeProvider)
{
    public async Task CreateAsync(
        ContextEvent contextEvent,
        IReadOnlyList<PlayerContextImpact> impacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextEvent);
        ArgumentNullException.ThrowIfNull(impacts);
        if (contextEvent.Verification != VerificationState.Proposed)
        {
            throw new InvalidOperationException(
                "Only proposed context events can be created.");
        }

        if (impacts.Count == 0
            || impacts.Any(impact =>
                impact.ContextEventId != contextEvent.Id
                || !contextEvent.AffectedPlayerIds.Contains(impact.PlayerId)))
        {
            throw new ArgumentException(
                "Context impacts must cover players affected by the event.",
                nameof(impacts));
        }

        await repository.AddAsync(contextEvent, impacts, cancellationToken);
    }

    public async Task VerifyAsync(
        Guid contextEventId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var contextEvent = await RequireEventAsync(
            contextEventId,
            cancellationToken);
        if (contextEvent.Verification != VerificationState.Proposed)
        {
            throw new ResourceConflictException(
                "Only a proposed context event can be verified.");
        }

        contextEvent.VerifyByHuman(userId, timeProvider.GetUtcNow());
        await repository.SaveAsync(contextEvent, cancellationToken);
    }

    public async Task RejectAsync(
        Guid contextEventId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var contextEvent = await RequireEventAsync(
            contextEventId,
            cancellationToken);
        if (contextEvent.Verification != VerificationState.Proposed)
        {
            throw new ResourceConflictException(
                "Only a proposed context event can be rejected.");
        }

        contextEvent.RejectByHuman(userId, timeProvider.GetUtcNow());
        await repository.SaveAsync(contextEvent, cancellationToken);
    }

    public async Task ExpireAsync(
        Guid contextEventId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var contextEvent = await RequireEventAsync(
            contextEventId,
            cancellationToken);
        contextEvent.ExpireByHuman(userId, timeProvider.GetUtcNow());
        await repository.SaveAsync(contextEvent, cancellationToken);
    }

    public async Task OverrideImpactAsync(
        Guid contextEventId,
        PlayerId playerId,
        ContextImpactOverride values,
        Guid userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(values);
        await RequireEventAsync(contextEventId, cancellationToken);
        var impact = await repository.GetImpactAsync(
            contextEventId,
            playerId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Context impact for event '{contextEventId}' and player was not found.");
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Override user id cannot be empty.",
                nameof(userId));
        }

        var overridden = impact.Override(
            values.MinutesDelta,
            values.UsageDelta,
            values.AssistShareDelta,
            values.ReboundShareDelta,
            values.ShotVolumeDelta,
            values.RoleRiskDelta,
            values.ProjectionConfidenceDelta);
        await repository.SaveImpactOverrideAsync(
            overridden,
            userId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<ContextEvent> RequireEventAsync(
        Guid contextEventId,
        CancellationToken cancellationToken) =>
        await repository.GetAsync(contextEventId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Context event '{contextEventId}' was not found.");
}
