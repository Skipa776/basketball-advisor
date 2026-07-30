using System.Text.RegularExpressions;

namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

public enum BasketballReferenceSeasonPage
{
    PerGame,
    Totals,
    Advanced,
}

public sealed partial class BbrefUrlBuilder
{
    private static readonly Uri BaseUri = new("https://www.basketball-reference.com");

    public Uri CreateSeasonUri(int seasonEndYear, BasketballReferenceSeasonPage page)
    {
        if (seasonEndYear < 1947)
        {
            throw new ArgumentOutOfRangeException(nameof(seasonEndYear));
        }

        var suffix = page switch
        {
            BasketballReferenceSeasonPage.PerGame => "per_game",
            BasketballReferenceSeasonPage.Totals => "totals",
            BasketballReferenceSeasonPage.Advanced => "advanced",
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
        return CreateUri($"/leagues/NBA_{seasonEndYear}_{suffix}.html");
    }

    public Uri CreateUri(string pathOrUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathOrUri);
        var uri = pathOrUri.StartsWith("/", StringComparison.Ordinal)
            ? new Uri(BaseUri, pathOrUri)
            : Uri.TryCreate(pathOrUri, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(BaseUri, pathOrUri);

        if (uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(
                uri.Host,
                BaseUri.Host,
                StringComparison.OrdinalIgnoreCase)
            || uri.Port != BaseUri.Port
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (!SeasonPath().IsMatch(uri.AbsolutePath)
                && !PlayerMetadataPath().IsMatch(uri.AbsolutePath)))
        {
            throw new InvalidOperationException(
                $"Basketball-Reference path '{pathOrUri}' is not allowlisted.");
        }

        return uri;
    }

    [GeneratedRegex(
        "^/leagues/NBA_[0-9]{4}_(per_game|totals|advanced)\\.html$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SeasonPath();

    [GeneratedRegex(
        "^/players/[a-z]/[a-z0-9]+\\.html$",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlayerMetadataPath();
}
