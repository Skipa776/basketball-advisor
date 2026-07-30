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

    Task AddAdjustedAsync(
        AdjustedProjection adjusted,
        CancellationToken cancellationToken);

    Task<AdjustedProjection?> GetAdjustedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddFantasyValueAsync(
        FantasyValue value,
        CancellationToken cancellationToken);

    Task<FantasyValue?> GetFantasyValueAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken);
}
