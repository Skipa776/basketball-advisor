using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Players;

namespace FantasyBasketball.Api.Endpoints;

public sealed record ScheduleImportRequest(DateOnly From, DateOnly To);

public sealed record SeasonStatsImportRequest(int SeasonEndYear);

public sealed record AdpImportRequest(string? Csv);

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports");
        group.MapPost("/players", ImportPlayersAsync);
        group.MapPost("/schedule", ImportScheduleAsync);
        group.MapPost("/season-stats", ImportSeasonStatsAsync);
        group.MapPost("/adp", ImportAdpAsync);
        group.MapGet("/runs", ListRunsAsync);
        return endpoints;
    }

    public static async Task<IResult> ImportPlayersAsync(
        IImportJobQueue queue,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await queue.EnqueueAsync(
                new ImportJobRequest(ImportJobKind.Players),
                cancellationToken),
            StatusCodes.Status202Accepted);

    public static async Task<IResult> ImportScheduleAsync(
        ScheduleImportRequest request,
        IImportJobQueue queue,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await queue.EnqueueAsync(
                new ImportJobRequest(
                    ImportJobKind.Schedule,
                    From: request.From,
                    To: request.To),
                cancellationToken),
            StatusCodes.Status202Accepted);

    public static async Task<IResult> ImportSeasonStatsAsync(
        SeasonStatsImportRequest request,
        IImportJobQueue queue,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await queue.EnqueueAsync(
                new ImportJobRequest(
                    ImportJobKind.SeasonStats,
                    SeasonEndYear: request.SeasonEndYear),
                cancellationToken),
            StatusCodes.Status202Accepted);

    public static async Task<IResult> ImportAdpAsync(
        AdpImportRequest request,
        IImportJobQueue queue,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await queue.EnqueueAsync(
                new ImportJobRequest(ImportJobKind.Adp, Csv: request.Csv),
                cancellationToken),
            StatusCodes.Status202Accepted);

    public static async Task<IResult> ListRunsAsync(
        int page,
        int limit,
        ImportRunQueryService service,
        CancellationToken cancellationToken)
    {
        page = page == 0 ? 1 : page;
        limit = limit == 0 ? Paging.DefaultLimit : limit;
        var result = await service.ListAsync(page, limit, cancellationToken);
        return ApiResults.Success(
            result.Items,
            meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }
}
