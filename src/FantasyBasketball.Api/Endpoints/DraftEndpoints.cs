using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Api.Middleware;

namespace FantasyBasketball.Api.Endpoints;

public sealed record CreateDraftRequest(
    Guid LeagueId,
    int DraftPosition,
    int RoundCount);

public sealed record RecordDraftPickRequest(
    int PickNumber,
    Guid PlayerId);

public static class DraftEndpoints
{
    public static IEndpointRouteBuilder MapDraftEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/drafts");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization();
        group.MapPost("/", CreateAsync)
            .WithMetadata(new OwnedRouteMetadata("league", "body:leagueId"));
        group.MapGet("/{id:guid}/board", GetBoardAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapPost("/{id:guid}/picks", RecordPickAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapDelete("/{id:guid}/picks/{pickNumber:int}", UndoPickAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        group.MapGet("/{id:guid}/recommendations", GetRecommendationsAsync)
            .WithMetadata(new OwnedRouteMetadata("draft", "id"));
        return endpoints;
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
