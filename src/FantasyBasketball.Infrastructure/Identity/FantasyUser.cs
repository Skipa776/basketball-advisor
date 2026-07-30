using Microsoft.AspNetCore.Identity;

namespace FantasyBasketball.Infrastructure.Identity;

public sealed class FantasyUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsInstanceOwner { get; set; }
}
