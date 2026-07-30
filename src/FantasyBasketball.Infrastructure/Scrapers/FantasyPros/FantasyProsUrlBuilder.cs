namespace FantasyBasketball.Infrastructure.Scrapers.FantasyPros;

public sealed class FantasyProsUrlBuilder
{
    private static readonly Uri BaseUri = new("https://www.fantasypros.com");
    private const string AdpPath = "/nba/adp/overall.php";

    public Uri CreateAdpUri() => CreateUri(AdpPath);

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
            || !string.Equals(uri.AbsolutePath, AdpPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"FantasyPros path '{pathOrUri}' is not allowlisted.");
        }

        return uri;
    }
}
