using FantasyBasketball.Application.Draft;
using FantasyBasketball.Domain.Players;

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
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}/board", GetBoardAsync);
        group.MapPost("/{id:guid}/picks", RecordPickAsync);
        group.MapDelete("/{id:guid}/picks/{pickNumber:int}", UndoPickAsync);
        group.MapGet("/{id:guid}/recommendations", GetRecommendationsAsync);
        return endpoints;
    }

    public static async Task<IResult> CreateAsync(
        CreateDraftRequest request,
        DraftSessionService service,
        CancellationToken cancellationToken) =>
        ApiResults.Success(
            await service.CreateAsync(
                request.LeagueId,
                request.DraftPosition,
                request.RoundCount,
                cancellationToken),
            StatusCodes.Status201Created);

    public static async Task<IResult> GetBoardAsync(
        Guid id,
        Guid leagueId,
        DraftSessionService sessions,
        DraftBoardService boards,
        CancellationToken cancellationToken)
    {
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
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.RecordPickAsync(
            id,
            request.PickNumber,
            new PlayerId(request.PlayerId),
            cancellationToken));

    public static async Task<IResult> UndoPickAsync(
        Guid id,
        int pickNumber,
        DraftSessionService service,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.UndoPickAsync(
            id,
            pickNumber,
            cancellationToken));

    public static async Task<IResult> GetRecommendationsAsync(
        Guid id,
        DraftBoardService service,
        CancellationToken cancellationToken) =>
        ApiResults.Success(await service.GetRecommendationsAsync(
            id,
            cancellationToken));
}
