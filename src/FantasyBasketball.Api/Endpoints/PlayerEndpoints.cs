using FantasyBasketball.Application.Players;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Identity;

namespace FantasyBasketball.Api.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/players");
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/projection", GetProjectionAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "query:leagueId"));
        return endpoints;
    }

    public static async Task<IResult> ListAsync(
        string? search,
        string? team,
        string? position,
        int page,
        int limit,
        PlayerQueryService service,
        CancellationToken cancellationToken)
    {
        page = page == 0 ? 1 : page;
        limit = limit == 0 ? Paging.DefaultLimit : limit;
        var result = await service.ListAsync(
            search,
            team,
            position,
            page,
            limit,
            cancellationToken);
        return ApiResults.Success(
            result.Items,
            meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }

    public static async Task<IResult> GetAsync(
        Guid id,
        PlayerQueryService service,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.GetAsync(
            new PlayerId(id),
            cancellationToken));

    public static async Task<IResult> GetProjectionAsync(
        Guid id,
        Guid leagueId,
        ProjectionDecompositionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await RequireAndGetProjectionAsync(
            id,
            leagueId,
            service,
            authorization,
            cancellationToken));

    private static async Task<ProjectionDecomposition> RequireAndGetProjectionAsync(
        Guid id,
        Guid leagueId,
        ProjectionDecompositionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(leagueId, cancellationToken);
        return await service.GetAsync(
            new PlayerId(id),
            leagueId,
            cancellationToken);
    }
}
