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
        entries[0].Provenance.ParserVersion.ShouldBe("fantasypros-v1");
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
