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
