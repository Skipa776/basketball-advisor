using FantasyBasketball.Api.Middleware;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Providers.Sleeper;

namespace FantasyBasketball.Api.Endpoints;

public sealed record CreateDraftRequest(
    Guid LeagueId,
    int DraftPosition,
    int RoundCount);

public sealed record RecordDraftPickRequest(
    int PickNumber,
    Guid PlayerId);

public sealed record TakenPicksRequest(IReadOnlyList<string> Names);

public sealed record SleeperSyncRequest(string? SleeperLeagueId);

public static class DraftEndpoints
{
    public static IEndpointRouteBuilder MapDraftEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/drafts");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization();
        group.MapGet("/", ListAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "query:leagueId"));
        group.MapGet("/{id:guid}", GetAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/", CreateAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "body:leagueId"));
        group.MapGet("/{id:guid}/board", GetBoardAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/{id:guid}/picks", RecordPickAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapDelete("/{id:guid}/picks/{pickNumber:int}", UndoPickAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/{id:guid}/picks/simulate", SimulateAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/{id:guid}/picks/taken", RecordTakenAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/{id:guid}/picks/sync", SyncPicksAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapGet("/{id:guid}/recommendations", GetRecommendationsAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        return endpoints;
    }

    public static async Task<IResult> ListAsync(
        Guid? leagueId,
        int? page,
        int? limit,
        DraftSessionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        if (leagueId is null || leagueId == Guid.Empty)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["leagueId"] = ["Choose a league to list its drafts."],
            });
        }

        await authorization.RequireLeagueAsync(leagueId.Value, cancellationToken);
        var currentPage = page ?? 1;
        var pageSize = limit ?? Paging.DefaultLimit;
        Paging.Validate(currentPage, pageSize);
        var result = await service.ListAsync(
            leagueId.Value,
            currentPage,
            pageSize,
            cancellationToken);
        return ApiResults.Success(
            result.Items,
            meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }

    public static async Task<IResult> GetAsync(
        Guid id,
        DraftSessionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        return ApiResults.Success(await service.GetAsync(id, cancellationToken));
    }

    public static async Task<IResult> CreateAsync(
        CreateDraftRequest request,
        DraftSessionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireLeagueAsync(
            request.LeagueId,
            cancellationToken);
        return ApiResults.Success(
            await service.CreateAsync(
                request.LeagueId,
                request.DraftPosition,
                request.RoundCount,
                cancellationToken),
            StatusCodes.Status201Created);
    }

    public static async Task<IResult> GetBoardAsync(
        Guid id,
        Guid leagueId,
        DraftSessionService sessions,
        DraftBoardService boards,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        var session = await sessions.GetAsync(id, cancellationToken);
        if (session.LeagueId != leagueId)
        {
            throw new RequestValidationException(
                new Dictionary<string, string[]>
                {
                    ["leagueId"] = [
                        "League id does not match the draft session.",
                    ],
                });
        }

        return ApiResults.Success(await boards.GetBoardAsync(
            id,
            cancellationToken));
    }

    public static async Task<IResult> RecordPickAsync(
        Guid id,
        RecordDraftPickRequest request,
        DraftSessionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        return ApiResults.Success(await service.RecordPickAsync(
            id,
            request.PickNumber,
            new PlayerId(request.PlayerId),
            cancellationToken));
    }

    public static async Task<IResult> UndoPickAsync(
        Guid id,
        int pickNumber,
        DraftSessionService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        return ApiResults.Success(await service.UndoPickAsync(
            id,
            pickNumber,
            cancellationToken));
    }

    public static async Task<IResult> SimulateAsync(
        Guid id,
        DraftAssistService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        return ApiResults.Success(new { Picks = await service.SimulateToUserTurnAsync(id, cancellationToken) });
    }

    public static async Task<IResult> RecordTakenAsync(
        Guid id,
        TakenPicksRequest request,
        DraftAssistService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        if (request.Names is not { Count: > 0 and <= 400 })
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["names"] = ["Paste between 1 and 400 player names, one per line."],
            });
        }

        return ApiResults.Success(await service.RecordTakenAsync(id, request.Names, cancellationToken));
    }

    public static async Task<IResult> SyncPicksAsync(
        Guid id,
        SleeperSyncRequest request,
        DraftAssistService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        var sleeperLeagueId = request.SleeperLeagueId?.Trim() ?? string.Empty;
        if (!SleeperUrlBuilder.IsLeagueId(sleeperLeagueId))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["sleeperLeagueId"] = ["A Sleeper league id is the number in sleeper.com/leagues/<id>."],
            });
        }

        // Provider failures are the user's input or the provider's answer, never a 500.
        try
        {
            return ApiResults.Success(await service.SyncFromSleeperAsync(id, sleeperLeagueId, cancellationToken));
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidDataException)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["sleeper"] = [exception.Message] });
        }
        catch (HttpRequestException)
        {
            throw new RequestValidationException(new Dictionary<string, string[]> { ["sleeper"] = ["Sleeper did not answer. Try again in a minute."] });
        }
    }

    public static async Task<IResult> GetRecommendationsAsync(
        Guid id,
        DraftBoardService service,
        OwnedResourceAuthorizationService authorization,
        CancellationToken cancellationToken)
    {
        await authorization.RequireDraftAsync(id, cancellationToken);
        return ApiResults.Success(await service.GetRecommendationsAsync(
            id,
            cancellationToken));
    }
}
