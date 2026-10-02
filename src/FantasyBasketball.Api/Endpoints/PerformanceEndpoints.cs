using System.Globalization;
using FantasyBasketball.Api.Middleware;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Application.Trends;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Identity;

namespace FantasyBasketball.Api.Endpoints;

public static class PerformanceEndpoints
{
    public static async Task<IResult> PoolsAsync(Guid id, PlayerPerformanceService service,
        OwnedResourceAuthorizationService authorization, HttpContext context, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        return ApiResults.Success(await service.ListPoolsAsync(id, cancellationToken));
    }

    public static async Task<IResult> QueryAsync(Guid id, int? seasonEndYear, string? source,
        string? throughDate, string? view, int? page, int? limit, PlayerPerformanceService service,
        OwnedResourceAuthorizationService authorization, TimeProvider clock, HttpContext context, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        if (seasonEndYear is null or < 1947 || seasonEndYear > clock.GetUtcNow().Year + 1
            || string.IsNullOrWhiteSpace(source) || !DataSourceName.IsKnown(source)
            || !DateOnly.TryParseExact(throughDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)
            || view is not ("best" or "hot" or "all"))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["performance"] = ["Select a valid season, known source, game date on or before today (YYYY-MM-DD), and best, hot or all view."],
            });
        }

        var currentPage = page ?? 1;
        var pageSize = limit ?? Paging.DefaultLimit;
        Paging.Validate(currentPage, pageSize);
        var result = await service.QueryAsync(id, seasonEndYear.Value, source, date, view, currentPage, pageSize, cancellationToken);
        return ApiResults.Success(result, meta: new ApiMeta(result.Total, currentPage, pageSize));
    }

    public static async Task<IResult> LabelsAsync(Guid id, int? seasonEndYear, string? source, string? throughDate,
        HeatLabelService service, OwnedResourceAuthorizationService authorization, TimeProvider clock,
        HttpContext context, CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(id, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        if (seasonEndYear is null or < 1947 || seasonEndYear > clock.GetUtcNow().Year + 1
            || string.IsNullOrWhiteSpace(source) || !DataSourceName.IsKnown(source)
            || !DateOnly.TryParseExact(throughDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || date > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["heatLabels"] = ["Select a valid season, known source and game date on or before today (YYYY-MM-DD)."],
            });
        }

        return ApiResults.Success(await service.QueryAsync(id, seasonEndYear.Value, source, date, cancellationToken));
    }
}
