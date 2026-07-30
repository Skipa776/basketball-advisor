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
        int? page,
        int? limit,
        PlayerQueryService service,
        CancellationToken cancellationToken)
    {
        // These are nullable because a non-nullable int the caller omits is a
        // minimal-API binding failure, not a zero: GET /api/players with no
        // query string answered 400 instead of the documented default page.
        var result = await service.ListAsync(
            search,
            team,
            position,
            page ?? 1,
            limit ?? Paging.DefaultLimit,
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
