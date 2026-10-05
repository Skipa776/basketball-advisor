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

    /// <summary>
    /// <see cref="ListForPlayerAsync"/> for many players in one read, keyed by player (players
    /// with no events are absent); the default loops it.
    /// </summary>
    async Task<IReadOnlyDictionary<PlayerId, IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>>
        ListForPlayersAsync(IReadOnlyCollection<PlayerId> playerIds, CancellationToken cancellationToken)
    {
        var events = new Dictionary<PlayerId, IReadOnlyList<(ContextEvent Event, PlayerContextImpact Impact)>>();
        foreach (var playerId in playerIds)
        {
            var list = await ListForPlayerAsync(playerId, cancellationToken);
            if (list.Count > 0)
            {
                events[playerId] = list;
            }
        }

        return events;
    }

    Task SaveAsync(
        ContextEvent contextEvent,
        CancellationToken cancellationToken);

    Task SaveImpactOverrideAsync(
        PlayerContextImpact impact,
        Guid userId,
        DateTimeOffset overriddenAt,
        CancellationToken cancellationToken);
}
