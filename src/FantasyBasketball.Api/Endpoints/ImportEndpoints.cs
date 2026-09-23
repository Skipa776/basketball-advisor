using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Api.Middleware;

namespace FantasyBasketball.Api.Endpoints;

public sealed record ScheduleImportRequest(DateOnly From, DateOnly To);

/// <summary>The owner asserts the range is regular season; phase is never inferred.</summary>
public sealed record BoxScoreImportRequest(DateOnly From, DateOnly To);

public sealed record SeasonStatsImportRequest(int SeasonEndYear);

public sealed record AdpImportRequest(string? Csv);

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/imports");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization(policy => policy.RequireRole("Owner"));
        group.MapPost("/players", ImportPlayersAsync);
        group.MapPost("/schedule", ImportScheduleAsync);
        group.MapPost("/season-stats", ImportSeasonStatsAsync);
        group.MapPost("/adp", ImportAdpAsync);
        group.MapPost("/box-scores", ImportBoxScoresAsync);
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

    public static async Task<IResult> ImportBoxScoresAsync(
        BoxScoreImportRequest request,
        IImportJobQueue queue,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await queue.EnqueueAsync(
                new ImportJobRequest(
                    ImportJobKind.BoxScores,
                    From: request.From,
                    To: request.To),
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
        int? page,
        int? limit,
        ImportRunQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            page ?? 1,
            limit ?? Paging.DefaultLimit,
            cancellationToken);
        return ApiResults.Success(
            result.Items,
            meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }
}
