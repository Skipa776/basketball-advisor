using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public interface IPlayerQueryRepository
{
    Task<PagedResult<Player>> ListAsync(
        string? search,
        string? team,
        string? position,
        int page,
        int limit,
        CancellationToken cancellationToken);
}

public interface IProjectionQueryRepository
{
    Task<ProjectionDecomposition?> GetLatestDecompositionAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken);
}

public interface IImportRunQueryRepository
{
    Task<PagedResult<DataImportRun>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DataImportRun>> ListRecentAsync(
        CancellationToken cancellationToken);
}

public interface IContextEventQueryRepository
{
    Task<PagedResult<ContextEvent>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken);
}
