using FantasyBasketball.Application.Context;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Api.Middleware;

namespace FantasyBasketball.Api.Endpoints;

public sealed record CreateContextEventRequest(
    string Type,
    Guid? TeamId,
    Guid? PrimaryPlayerId,
    IReadOnlyList<Guid>? AffectedPlayerIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? ExpectedExpiration,
    string Direction,
    decimal Magnitude,
    string Confidence,
    string? SourceUrl,
    string? SourceName,
    string? RawText,
    string Summary);

public sealed record HumanReviewRequest(Guid UserId);

public static class ContextEndpoints
{
    public static IEndpointRouteBuilder MapContextEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/context-events");
        group.AddEndpointFilter<CookieAntiforgeryFilter>();
        group.RequireAuthorization();
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPost("/{id:guid}/verify", VerifyAsync)
            .WithMetadata(new OwnedRouteMetadata("context-event", "id"));
        group.MapPost("/{id:guid}/reject", RejectAsync)
            .WithMetadata(new OwnedRouteMetadata("context-event", "id"));
        return endpoints;
    }

    public static async Task<IResult> ListAsync(
        int? page,
        int? limit,
        ContextEventQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            page ?? 1,
            limit ?? Paging.DefaultLimit,
            cancellationToken);
        return ApiResults.Success(
            result.Items.Select(contextEvent => new
            {
                contextEvent.Id,
                contextEvent.Type,
                TypeName = contextEvent.Type.ToString(),
                contextEvent.TeamId,
                contextEvent.PrimaryPlayerId,
                contextEvent.AffectedPlayerIds,
                contextEvent.CreatedAt,
                contextEvent.EffectiveFrom,
                contextEvent.ExpectedExpiration,
                contextEvent.Direction,
                DirectionName = contextEvent.Direction.ToString(),
                contextEvent.Magnitude,
                contextEvent.Confidence,
                ConfidenceName = contextEvent.Confidence.ToString(),
                contextEvent.SourceUrl,
                contextEvent.SourceName,
                contextEvent.RawText,
                contextEvent.Summary,
                contextEvent.Verification,
                VerificationName = contextEvent.Verification.ToString(),
            }),
            meta: new ApiMeta(result.Total, result.Page, result.Limit));
    }

    public static async Task<IResult> CreateAsync(
        CreateContextEventRequest request,
        ContextEventService service,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var affected = request.AffectedPlayerIds?
            .Select(value => new PlayerId(value))
            .ToArray() ?? [];
        var fields = new Dictionary<string, string[]>();
        if (!Enum.TryParse<ContextEventType>(request.Type, true, out var type))
        {
            fields["type"] = [$"Unknown context event type '{request.Type}'."];
        }

        if (!Enum.TryParse<ContextDirection>(
                request.Direction,
                true,
                out var direction))
        {
            fields["direction"] = [
                $"Unknown context direction '{request.Direction}'.",
            ];
        }

        if (!Enum.TryParse<Confidence>(
                request.Confidence,
                true,
                out var confidence))
        {
            fields["confidence"] = [
                $"Unknown confidence '{request.Confidence}'.",
            ];
        }

        if (affected.Length == 0)
        {
            fields["affectedPlayerIds"] = [
                "At least one affected player is required.",
            ];
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            fields["summary"] = ["Summary is required."];
        }

        if (fields.Count > 0)
        {
            throw new RequestValidationException(fields);
        }

        var now = timeProvider.GetUtcNow();
        var contextEvent = ContextEvent.Create(
            Guid.NewGuid(),
            type,
            request.TeamId is { } teamId ? new NbaTeamId(teamId) : null,
            request.PrimaryPlayerId is { } playerId
                ? new PlayerId(playerId)
                : null,
            affected,
            now,
            request.EffectiveFrom,
            direction,
            request.Magnitude,
            confidence,
            request.SourceUrl,
            string.IsNullOrWhiteSpace(request.SourceName)
                ? DataSourceName.Manual
                : request.SourceName,
            request.RawText,
            request.Summary,
            request.ExpectedExpiration);
        var impacts = affected.Select(playerId =>
            PlayerContextImpact.CreateDefault(
                Guid.NewGuid(),
                contextEvent,
                playerId)).ToArray();
        await service.CreateAsync(
            contextEvent,
            impacts,
            cancellationToken);
        return ApiResults.Success(
            contextEvent,
            StatusCodes.Status201Created);
    }

    public static async Task<IResult> VerifyAsync(
        Guid id,
        HumanReviewRequest request,
        ContextEventService service,
        OwnedResourceAuthorizationService authorization,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        await authorization.RequireContextEventAsync(id, cancellationToken);
        await service.VerifyAsync(
            id,
            userContext.CurrentUserId,
            cancellationToken);
        return ApiResults.Success(new { Id = id, State = nameof(VerificationState.Verified) });
    }

    public static async Task<IResult> RejectAsync(
        Guid id,
        HumanReviewRequest request,
        ContextEventService service,
        OwnedResourceAuthorizationService authorization,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        await authorization.RequireContextEventAsync(id, cancellationToken);
        await service.RejectAsync(
            id,
            userContext.CurrentUserId,
            cancellationToken);
        return ApiResults.Success(new { Id = id, State = nameof(VerificationState.Rejected) });
    }
}
