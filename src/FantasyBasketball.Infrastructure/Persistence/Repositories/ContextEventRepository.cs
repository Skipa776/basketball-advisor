using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class ContextEventRepository(FantasyDbContext database)
    : IContextEventRepository, IContextEventQueryRepository
{
    public async Task AddAsync(
        ContextEvent contextEvent,
        IReadOnlyList<PlayerContextImpact> impacts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextEvent);
        ArgumentNullException.ThrowIfNull(impacts);
        if (contextEvent.Verification != VerificationState.Proposed)
        {
            throw new InvalidOperationException(
                "Only proposed context events can be inserted.");
        }

        database.ContextEvents.Add(Map(contextEvent));
        database.PlayerContextImpacts.AddRange(impacts.Select(Map));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<ContextEvent?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.ContextEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<PlayerContextImpact?> GetImpactAsync(
        Guid contextEventId,
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        var row = await database.PlayerContextImpacts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.ContextEventId == contextEventId
                    && value.PlayerId == playerId.Value,
                cancellationToken);
        if (row is null)
        {
            return null;
        }

        var eventRow = await database.ContextEvents
            .AsNoTracking()
            .SingleAsync(value => value.Id == contextEventId, cancellationToken);
        return Map(row, Map(eventRow));
    }

    public async Task<IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>
        ListForPlayerAsync(
            PlayerId playerId,
            CancellationToken cancellationToken) =>
        (await ListForPlayersAsync([playerId], cancellationToken)).GetValueOrDefault(playerId) ?? [];

    public async Task<IReadOnlyDictionary<PlayerId, IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>>
        ListForPlayersAsync(
            IReadOnlyCollection<PlayerId> playerIds,
            CancellationToken cancellationToken)
    {
        var ids = playerIds.Select(id => id.Value).ToArray();
        var rows = await database.PlayerContextImpacts
            .AsNoTracking()
            .Where(value => ids.Contains(value.PlayerId))
            .Join(
                database.ContextEvents.AsNoTracking(),
                impact => impact.ContextEventId,
                contextEvent => contextEvent.Id,
                (impact, contextEvent) => new { Impact = impact, Event = contextEvent })
            .OrderBy(value => value.Event.EffectiveFrom)
            .ThenBy(value => value.Event.Id)
            .ToArrayAsync(cancellationToken);
        return rows
            .GroupBy(value => new PlayerId(value.Impact.PlayerId))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>)group
                    .Select(value =>
                    {
                        var contextEvent = Map(value.Event);
                        return (contextEvent, Map(value.Impact, contextEvent));
                    })
                    .ToArray());
    }

    public async Task SaveAsync(
        ContextEvent contextEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextEvent);
        var row = await database.ContextEvents.SingleOrDefaultAsync(
            value => value.Id == contextEvent.Id,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Context event '{contextEvent.Id}' was not found.");
        row.ApplyReview(
            contextEvent.ExpectedExpiration,
            contextEvent.Verification.ToString(),
            contextEvent.ReviewedByUserId,
            contextEvent.VerifiedByUserId,
            contextEvent.VerifiedAt,
            contextEvent.ReviewedAt);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveImpactOverrideAsync(
        PlayerContextImpact impact,
        Guid userId,
        DateTimeOffset overriddenAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(impact);
        if (!impact.IsOverridden)
        {
            throw new InvalidOperationException(
                "Only a user-overridden impact can replace persisted defaults.");
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Override user id cannot be empty.",
                nameof(userId));
        }

        if (overriddenAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Override timestamp must be UTC.",
                nameof(overriddenAt));
        }

        var row = await database.PlayerContextImpacts.SingleOrDefaultAsync(
            value => value.Id == impact.Id,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Context impact '{impact.Id}' was not found.");
        row.ApplyOverride(
            impact.MinutesDelta,
            impact.UsageDelta,
            impact.AssistShareDelta,
            impact.ReboundShareDelta,
            impact.ShotVolumeDelta,
            impact.RoleRiskDelta,
            impact.ProjectionConfidenceDelta,
            userId,
            overriddenAt);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<ContextEvent>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var total = await database.ContextEvents.CountAsync(cancellationToken);
        var rows = await database.ContextEvents
            .AsNoTracking()
            .OrderByDescending(value => value.EffectiveFrom)
            .ThenByDescending(value => value.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<ContextEvent>(
            rows.Select(Map).ToArray(),
            total,
            page,
            limit);
    }

    private static ContextEventRow Map(ContextEvent value) =>
        ContextEventRow.Create(
            value.Id,
            value.Type.ToString(),
            value.TeamId?.Value,
            value.PrimaryPlayerId?.Value,
            value.AffectedPlayerIds.Select(playerId => playerId.Value).ToArray(),
            value.CreatedAt,
            value.EffectiveFrom,
            value.ExpectedExpiration,
            value.Direction.ToString(),
            value.Magnitude,
            value.Confidence.ToString(),
            value.SourceUrl,
            value.SourceName,
            value.RawText,
            value.Summary,
            value.Verification.ToString());

    private static PlayerContextImpactRow Map(PlayerContextImpact value) =>
        PlayerContextImpactRow.Create(
            value.Id,
            value.ContextEventId,
            value.PlayerId.Value,
            value.MinutesDelta,
            value.UsageDelta,
            value.AssistShareDelta,
            value.ReboundShareDelta,
            value.ShotVolumeDelta,
            value.RoleRiskDelta,
            value.ProjectionConfidenceDelta,
            value.IsOverridden);

    private static ContextEvent Map(ContextEventRow row)
    {
        var contextEvent = ContextEvent.Create(
            row.Id,
            Enum.Parse<ContextEventType>(row.Type),
            row.TeamId is { } teamId ? new NbaTeamId(teamId) : null,
            row.PrimaryPlayerId is { } playerId ? new PlayerId(playerId) : null,
            row.AffectedPlayerIds.Select(value => new PlayerId(value)).ToArray(),
            row.CreatedAt,
            row.EffectiveFrom,
            Enum.Parse<ContextDirection>(row.Direction),
            row.Magnitude,
            Enum.Parse<Confidence>(row.Confidence),
            row.SourceUrl,
            row.SourceName,
            row.RawText,
            row.Summary,
            row.ExpectedExpiration);
        var state = Enum.Parse<VerificationState>(row.Verification);
        if (state == VerificationState.Verified)
        {
            contextEvent.VerifyByHuman(
                row.VerifiedByUserId
                    ?? throw new InvalidOperationException(
                        "Verified event has no verifying user."),
                row.VerifiedAt
                    ?? throw new InvalidOperationException(
                        "Verified event has no verification time."));
            if (row.ReviewedAt > row.VerifiedAt)
            {
                contextEvent.ExpireByHuman(
                    row.ReviewedByUserId
                        ?? throw new InvalidOperationException(
                            "Reviewed event has no reviewing user."),
                    row.ReviewedAt.Value);
            }
        }
        else if (state == VerificationState.Rejected)
        {
            contextEvent.RejectByHuman(
                row.ReviewedByUserId
                    ?? throw new InvalidOperationException(
                        "Rejected event has no reviewing user."),
                row.ReviewedAt
                    ?? throw new InvalidOperationException(
                        "Rejected event has no review time."));
        }
        else if (row.ReviewedAt is { } reviewedAt)
        {
            contextEvent.ExpireByHuman(
                row.ReviewedByUserId
                    ?? throw new InvalidOperationException(
                        "Reviewed event has no reviewing user."),
                reviewedAt);
        }

        return contextEvent;
    }

    private static PlayerContextImpact Map(
        PlayerContextImpactRow row,
        ContextEvent contextEvent) =>
        row.IsOverridden
            ? PlayerContextImpact.CreateOverride(
                row.Id,
                row.ContextEventId,
                new PlayerId(row.PlayerId),
                row.MinutesDelta,
                row.UsageDelta,
                row.AssistShareDelta,
                row.ReboundShareDelta,
                row.ShotVolumeDelta,
                row.RoleRiskDelta,
                row.ProjectionConfidenceDelta)
            : PlayerContextImpact.CreateDefault(
                row.Id,
                contextEvent,
                new PlayerId(row.PlayerId));
}
