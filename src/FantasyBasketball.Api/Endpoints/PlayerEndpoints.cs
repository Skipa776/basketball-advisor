using FantasyBasketball.Application.Players;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Api.Endpoints;

public static class PlayerEndpoints
{
    public static IEndpointRouteBuilder MapPlayerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/players");
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapGet("/{id:guid}/projection", GetProjectionAsync);
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
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.GetAsync(
            new PlayerId(id),
            leagueId,
            cancellationToken));
}
