namespace FantasyBasketball.Infrastructure.Identity;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public bool OpenRegistration { get; set; }
}
