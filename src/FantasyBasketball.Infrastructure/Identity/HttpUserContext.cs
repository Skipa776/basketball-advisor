using System.Security.Claims;
using FantasyBasketball.Domain.Accounts;
using Microsoft.AspNetCore.Http;

namespace FantasyBasketball.Infrastructure.Identity;

public sealed class HttpUserContext(IHttpContextAccessor accessor) : IUserContext
{
    public Guid CurrentUserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(
                ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) && userId != Guid.Empty
                ? userId
                : throw new InvalidOperationException(
                    "Owned data requires an authenticated request user.");
        }
    }
}

public sealed class MissingUserContext : IUserContext
{
    public static MissingUserContext Instance { get; } = new();

    private MissingUserContext()
    {
    }

    public Guid CurrentUserId =>
        throw new InvalidOperationException(
            "Owned data is unavailable outside an authenticated request scope.");
}
