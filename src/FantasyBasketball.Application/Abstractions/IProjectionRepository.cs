using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;

namespace FantasyBasketball.Application.Abstractions;

public interface IProjectionRepository
{
    Task AddAsync(
        ObservedStats observed,
        BaselineProjection baseline,
        CancellationToken cancellationToken);

    Task<ObservedStats?> GetLatestObservedAsync(
        PlayerId playerId,
        CancellationToken cancellationToken);

    Task<BaselineProjection?> GetBaselineAsync(
        Guid id,
        CancellationToken cancellationToken);
}
