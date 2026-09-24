using System.Net;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Scrapers.FantasyPros;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Scrapers;

public sealed class FantasyProsAdpScraperTests : IDisposable
{
    private readonly HttpClient client;

    public FantasyProsAdpScraperTests()
    {
        client = new HttpClient(new FixtureHandler())
        {
            BaseAddress = new Uri("https://www.fantasypros.com"),
        };
    }

    [Fact]
    public void S10_url_builder_allows_only_the_public_adp_page()
    {
        var builder = new FantasyProsUrlBuilder();

        builder.CreateAdpUri().AbsoluteUri.ShouldBe(
            "https://www.fantasypros.com/nba/adp/overall.php");
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri("/nba/adp/gamelog/"));
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri("/api/nba/adp"));
        Should.Throw<InvalidOperationException>(() =>
            builder.CreateUri("https://example.com/nba/adp/overall.php"));
    }

    [Fact]
    public async Task S13_parser_is_pure_and_schema_drift_fails_loudly()
    {
        var parser = new AdpTableParser();
        var html = await ReadFixtureAsync();

        var first = parser.Parse(html);
        var second = parser.Parse(html);

        first.Count.ShouldBe(2);
        second.Count.ShouldBe(2);
        first[0].ShouldBe(second[0]);
        first[0].ExternalPlayerId.ShouldBe("nikola-jokic");
        first[0].PlayerName.ShouldBe("Nikola Jokic");
        first[0].AverageDraftPosition.ShouldBe(2.3m);
        first[0].StandardDeviation.ShouldBe(1.1m);

        var malformed = html.Replace(
            "<th>AVG</th>",
            "<th>AVERAGE</th>",
            StringComparison.Ordinal);
        Should.Throw<InvalidDataException>(() => parser.Parse(malformed))
            .Message.ShouldContain("AVG");
    }

    // The live 2026-27 page (verified 2026-09-23): Rank, Player, Yahoo, ESPN, AVG with no
    // STD DEV, a "(TEAM - POS)" <small> after the player link, and rows left unclosed.
    [Fact]
    public void S13_current_page_layout_without_std_dev_and_with_team_suffix_parses()
    {
        const string html = """
            <table id="data"><thead><tr><th>Rank</th><th>Player</th><th>Yahoo</th><th>ESPN</th><th>AVG</th></tr></thead>
            <tbody>
            <tr><td>1</td><td class="player-label"><a href="/nba/adp/nikola-jokic.php">Nikola Jokic</a> <small>(DEN - C)</small></td><td>2</td><td>1</td><td>1.5</td>
            <tr><td>2</td><td class="player-label"><a href="/nba/adp/luka-doncic.php">Luka Doncic</a> <small>(LAL - PG,SG)</small></td><td>3</td><td>4</td><td>3.5</td>
            </tbody></table>
            """;

        var entries = new AdpTableParser().Parse(html);

        entries.Select(entry => (entry.ExternalPlayerId, entry.PlayerName, entry.AverageDraftPosition, entry.StandardDeviation))
            .ShouldBe([("nikola-jokic", "Nikola Jokic", 1.5m, (decimal?)null), ("luka-doncic", "Luka Doncic", 3.5m, (decimal?)null)]);
    }

    [Fact]
    public async Task Recorded_html_maps_to_provenance_complete_adp_entries()
    {
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        var scraper = new FantasyProsAdpScraper(
            new SingleClientFactory(client),
            timeProvider);

        var entries = await scraper.GetAdpAsync(
            TestContext.Current.CancellationToken);

        entries.Count.ShouldBe(2);
        entries[0].Provenance.Source.ShouldBe(DataSourceName.FantasyPros);
        entries[0].Provenance.ParserVersion.ShouldBe("fantasypros-v2");
        entries[0].Provenance.Confidence.ShouldBe(DataSourceConfidence.HtmlScraper);
        entries[0].Provenance.RawRecordHash.Length.ShouldBe(64);
    }

    public void Dispose() => client.Dispose();

    private static Task<string> ReadFixtureAsync() =>
        File.ReadAllTextAsync(
            Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Html",
                "fantasypros-adp-2026-07-29.html"),
            TestContext.Current.CancellationToken);

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.ShouldBe(DataSourceName.FantasyPros);
            return client;
        }
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.RequestUri!.AbsolutePath.ShouldBe("/nba/adp/overall.php");
            var html = await ReadFixtureAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html),
                RequestMessage = request,
            };
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
