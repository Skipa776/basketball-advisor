using FantasyBasketball.Application.Health;

namespace FantasyBasketball.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/health/data-sources", GetDataSourcesAsync);
        return endpoints;
    }

    public static async Task<IResult> GetDataSourcesAsync(
        DataSourceHealthService service,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.GetAsync(cancellationToken));
}
