namespace FantasyBasketball.Api.Options;

/// <summary>
/// Google sign-in is inert until an operator supplies their own credentials.
/// Nothing is shipped: a self-hosted artifact carrying a client secret would
/// hand every instance the same identity, and the secrets policy forbids it.
/// The button is hidden rather than rendered broken when this is unconfigured.
/// </summary>
public sealed class GoogleSignInOptions
{
    public const string SectionName = "GoogleSignIn";

    public string? ClientId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId);
}
