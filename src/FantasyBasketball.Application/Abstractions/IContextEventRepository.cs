using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Abstractions;

public interface IContextEventRepository
{
    Task AddAsync(
        ContextEvent contextEvent,
        IReadOnlyList<PlayerContextImpact> impacts,
        CancellationToken cancellationToken);

    Task<ContextEvent?> GetAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<PlayerContextImpact?> GetImpactAsync(
        Guid contextEventId,
        PlayerId playerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>
        ListForPlayerAsync(
            PlayerId playerId,
            CancellationToken cancellationToken);

    Task SaveAsync(
        ContextEvent contextEvent,
        CancellationToken cancellationToken);

    Task SaveImpactOverrideAsync(
        PlayerContextImpact impact,
        Guid userId,
        DateTimeOffset overriddenAt,
        CancellationToken cancellationToken);
}
