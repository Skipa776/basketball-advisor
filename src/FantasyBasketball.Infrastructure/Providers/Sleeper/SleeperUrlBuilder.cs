using System.Text.RegularExpressions;

namespace FantasyBasketball.Infrastructure.Providers.Sleeper;

/// <summary>Only the nine owner-authorized, NBA-validated read endpoints (scraping_policy).</summary>
public static partial class SleeperUrlBuilder
{
    public static bool IsLeagueId(string value) => LeagueId().IsMatch(value);

    public static string League(string leagueId) => Build($"/v1/league/{leagueId}");

    public static string Users(string leagueId) => Build($"/v1/league/{leagueId}/users");

    public static string Rosters(string leagueId) => Build($"/v1/league/{leagueId}/rosters");

    public static string Drafts(string leagueId) => Build($"/v1/league/{leagueId}/drafts");

    public static string Draft(string draftId) => Build($"/v1/draft/{draftId}");

    public static string DraftPicks(string draftId) => Build($"/v1/draft/{draftId}/picks");

    public const string Players = "/v1/players/nba";

    /// <summary>A user by username or id (resolves a draft-log seed).</summary>
    public static string User(string usernameOrId) => Build($"/v1/user/{usernameOrId}");

    /// <summary>A user's NBA drafts for one season (draft-log snowball).</summary>
    public static string UserDrafts(string userId, int season) => Build($"/v1/user/{userId}/drafts/nba/{season}");

    public static string Build(string path) =>
        Allowed().IsMatch(path) ? path : throw new InvalidOperationException($"Sleeper path '{path}' is not allowlisted.");

    [GeneratedRegex("^[0-9]{1,25}$", RegexOptions.CultureInvariant)]
    private static partial Regex LeagueId();

    [GeneratedRegex("^/v1/(league/[0-9]{1,25}(/users|/rosters|/drafts)?|draft/[0-9]{1,25}(/picks)?|players/nba|user/[A-Za-z0-9_]{1,40}|user/[0-9]{1,25}/drafts/nba/20[0-9]{2})$", RegexOptions.CultureInvariant)]
    private static partial Regex Allowed();
}
