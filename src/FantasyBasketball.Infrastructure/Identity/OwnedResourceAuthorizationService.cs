using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Identity;

public sealed class OwnedResourceAuthorizationService(
    FantasyDbContext database,
    IUserContext userContext)
{
    public Task RequireLeagueAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        RequireAsync(
            database.FantasyLeagues.AnyAsync(
                value => value.Id == id
                    && value.OwnerId == userContext.CurrentUserId,
                cancellationToken),
            cancellationToken);

    public Task RequireDraftAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        RequireAsync(
            database.DraftSessions.AnyAsync(
                value => value.Id == id
                    && value.OwnerId == userContext.CurrentUserId,
                cancellationToken),
            cancellationToken);

    public Task RequireContextEventAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        RequireAsync(
            database.ContextEvents.AnyAsync(
                value => value.Id == id
                    && value.OwnerId == userContext.CurrentUserId,
                cancellationToken),
            cancellationToken);

    private static async Task RequireAsync(
        Task<bool> authorization,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await authorization)
        {
            throw new ResourceNotFoundException(
                "The requested resource was not found.");
        }
    }
}
