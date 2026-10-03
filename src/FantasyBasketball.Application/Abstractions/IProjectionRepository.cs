using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Leagues;
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

    /// <summary>Stores a baseline's distribution beside it; the baseline row is never changed.</summary>
    Task AddDistributionAsync(
        Guid baselineProjectionId,
        ProjectionDistribution distribution,
        CancellationToken cancellationToken);

    Task AddAdjustedAsync(
        AdjustedProjection adjusted,
        CancellationToken cancellationToken);

    Task<AdjustedProjection?> GetAdjustedAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task AddFantasyValueAsync(
        FantasyValue value,
        FantasyLeague league,
        DateTimeOffset computedAt,
        Guid? publicationId,
        CancellationToken cancellationToken);

    Task<FantasyValue?> GetFantasyValueAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken);
}
