using System.Security.Cryptography;
using System.Text;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Infrastructure.Scrapers.BasketballReference;

public sealed class BasketballReferenceStatsScraper(
    IHttpClientFactory clientFactory,
    PlayerIdentityResolver identityResolver,
    IPlayerRepository players,
    TimeProvider timeProvider) : IPlayerStatsProvider
{
    private const string ParserVersion = "basketball-reference-v3";
    private readonly BbrefUrlBuilder urlBuilder = new();
    private readonly SeasonTableParser parser = new();

    public string Name => DataSourceName.BasketballReference;

    public DataSourceKind Kind => DataSourceKind.Scraper;

    public async Task<IReadOnlyList<SeasonStatLine>> GetSeasonStatsAsync(
        int seasonEndYear,
        CancellationToken cancellationToken)
    {
        var client = clientFactory.CreateClient(Name);
        var perGameHtml = await client.GetStringAsync(
            urlBuilder.CreateSeasonUri(
                seasonEndYear,
                BasketballReferenceSeasonPage.PerGame),
            cancellationToken);
        var totalsHtml = await client.GetStringAsync(
            urlBuilder.CreateSeasonUri(
                seasonEndYear,
                BasketballReferenceSeasonPage.Totals),
            cancellationToken);
        var advancedHtml = await client.GetStringAsync(
            urlBuilder.CreateSeasonUri(
                seasonEndYear,
                BasketballReferenceSeasonPage.Advanced),
            cancellationToken);
        var parsedLines = parser.Parse(perGameHtml, totalsHtml, advancedHtml);
        var results = new List<SeasonStatLine>(parsedLines.Count);

        foreach (var parsed in parsedLines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var provenance = CreateProvenance(parsed);
            var resolution = await identityResolver.ResolveAsync(
                new ExternalPlayer(
                    parsed.ExternalPlayerId,
                    parsed.PlayerName,
                    null,
                    parsed.Position is { } primary ? [primary] : [],
                    null,
                    provenance),
                cancellationToken);
            if (resolution.Player is null)
            {
                continue;
            }

            if (parsed.Position is { } position && !resolution.Player.Positions.SequenceEqual([position]))
            {
                await players.SavePositionsAsync(resolution.Player.Id, [position], cancellationToken);
            }

            results.Add(new SeasonStatLine(
                resolution.Player.Id,
                seasonEndYear,
                parsed.GamesPlayed,
                parsed.MinutesPerGame,
                parsed.PerGame,
                parsed.Totals,
                parsed.UsageRate,
                provenance));
        }

        return results;
    }

    private DataProvenance CreateProvenance(ParsedSeasonStatLine parsed) =>
        new(
            DataSourceName.BasketballReference,
            parsed.ExternalPlayerId,
            timeProvider.GetUtcNow(),
            null,
            ParserVersion,
            DataSourceConfidence.HtmlScraper,
            Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(parsed.RawFragment)))
                .ToLowerInvariant());
}
