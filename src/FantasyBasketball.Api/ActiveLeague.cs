using Microsoft.AspNetCore.Http;

namespace FantasyBasketball.Api;

/// <summary>
/// The league the user is currently working in, held in a cookie.
///
/// A cookie rather than a column because this is view state, not owned data:
/// it decides which league a page opens on, and losing it costs one click. A
/// schema change would make a preference look like a fact, and the migration
/// would outlive the preference.
///
/// It is never trusted as authorisation. The value is a league id the user
/// picked from their own list, and every query that uses it still runs through
/// the ownership filter — a forged cookie selects a league the user cannot
/// read, and gets nothing.
/// </summary>
public sealed class ActiveLeague(IHttpContextAccessor accessor)
{
    public const string CookieName = "fb.league";

    public Guid? Current =>
        Guid.TryParse(
            accessor.HttpContext?.Request.Cookies[CookieName],
            out var leagueId)
            ? leagueId
            : null;

    /// <summary>
    /// The league a page should actually open on, given the leagues the user
    /// owns. The cookie wins when it names one of them; otherwise the first.
    ///
    /// Without this the app contradicted itself on first run: the switcher in
    /// the bar renders a league because a select always shows its first option,
    /// while every page read the cookie, found nothing, and reported "no active
    /// league" underneath a bar naming one. The cookie is only written when the
    /// user actively switches, which most users never need to do.
    ///
    /// It also drops a cookie pointing at a league that no longer exists, which
    /// otherwise left the app permanently insisting on a league the user could
    /// not see.
    /// </summary>
    public Guid? Resolve(IEnumerable<Guid> owned)
    {
        var candidates = owned as IReadOnlyCollection<Guid> ?? [.. owned];
        return Current is { } current && candidates.Contains(current)
            ? current
            : candidates.Select(league => (Guid?)league).FirstOrDefault();
    }

    public static CookieOptions Options(bool secure) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = secure,
        MaxAge = TimeSpan.FromDays(365),
        Path = "/",
    };
}
