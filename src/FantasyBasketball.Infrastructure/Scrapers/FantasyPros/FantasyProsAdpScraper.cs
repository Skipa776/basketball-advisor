using System.Security.Cryptography;
using System.Text;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Infrastructure.Scrapers.FantasyPros;

public sealed class FantasyProsAdpScraper(
    IHttpClientFactory clientFactory,
    TimeProvider timeProvider) : IAdpProvider
{
    private const string ParserVersion = "fantasypros-v2";
    private readonly FantasyProsUrlBuilder urlBuilder = new();
    private readonly AdpTableParser parser = new();

    public string Name => DataSourceName.FantasyPros;

    public DataSourceKind Kind => DataSourceKind.Scraper;

    public async Task<IReadOnlyList<AdpEntry>> GetAdpAsync(
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(Name);
        var html = await client.GetStringAsync(
            urlBuilder.CreateAdpUri(),
            cancellationToken);
        return parser.Parse(html)
            .Select(entry => new AdpEntry(
                entry.ExternalPlayerId,
                entry.PlayerName,
                entry.AverageDraftPosition,
                entry.StandardDeviation,
                CreateProvenance(entry)))
            .ToArray();
    }

    private DataProvenance CreateProvenance(ParsedAdpEntry entry) =>
        new(
            DataSourceName.FantasyPros,
            entry.ExternalPlayerId,
            timeProvider.GetUtcNow(),
            null,
            ParserVersion,
            DataSourceConfidence.HtmlScraper,
            Hash(entry.RawFragment));

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
