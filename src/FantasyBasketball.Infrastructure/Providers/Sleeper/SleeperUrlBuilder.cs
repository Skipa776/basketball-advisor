using System.Text.RegularExpressions;

namespace FantasyBasketball.Infrastructure.Providers.Sleeper;

/// <summary>Only the four owner-authorized, NBA-validated read endpoints (scraping_policy).</summary>
public static partial class SleeperUrlBuilder
{
    public static bool IsLeagueId(string value) => LeagueId().IsMatch(value);

    public static string League(string leagueId) => Build($"/v1/league/{leagueId}");

    public static string Users(string leagueId) => Build($"/v1/league/{leagueId}/users");

    public static string Rosters(string leagueId) => Build($"/v1/league/{leagueId}/rosters");

    public const string Players = "/v1/players/nba";

    public static string Build(string path) =>
        Allowed().IsMatch(path) ? path : throw new InvalidOperationException($"Sleeper path '{path}' is not allowlisted.");

    [GeneratedRegex("^[0-9]{1,25}$", RegexOptions.CultureInvariant)]
    private static partial Regex LeagueId();

    [GeneratedRegex("^/v1/(league/[0-9]{1,25}(/users|/rosters)?|players/nba)$", RegexOptions.CultureInvariant)]
    private static partial Regex Allowed();
}
