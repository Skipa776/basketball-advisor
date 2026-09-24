using System.Net;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Scrapers;

public sealed class BasketballReferenceStatsScraperTests : IDisposable
{
    private readonly HttpClient client;
    private readonly FakePlayerRepository players = new();
    private readonly DateTimeOffset fetchedAt =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    public BasketballReferenceStatsScraperTests()
    {
        client = new HttpClient(new FixtureHandler())
        {
            BaseAddress = new Uri("https://www.basketball-reference.com"),
        };
    }

    [Fact]
    public async Task I04_three_tables_map_to_canonical_season_stat_line()
    {
        var timeProvider = new FixedTimeProvider(fetchedAt);
        var scraper = new BasketballReferenceStatsScraper(
            new SingleClientFactory(client),
            new PlayerIdentityResolver(players, timeProvider),
            timeProvider);

        var stats = await scraper.GetSeasonStatsAsync(
            2026,
            TestContext.Current.CancellationToken);

        stats.Count.ShouldBe(1);
        var line = stats[0];
        line.SeasonEndYear.ShouldBe(2026);
        line.GamesPlayed.ShouldBe(72);
        line.MinutesPerGame.ShouldBe(34.5m);
        line.PerGame[StatKey.PTS].ShouldBe(27.1m);
        line.Totals[StatKey.REB].ShouldBe(885.6m);
        line.UsageRate.ShouldBe(0.245m);
        line.Provenance.Source.ShouldBe(DataSourceName.BasketballReference);
        line.Provenance.ExternalId.ShouldBe("jokicni01");
        line.Provenance.FetchedAt.ShouldBe(fetchedAt);
        line.Provenance.ParserVersion.ShouldBe("basketball-reference-v2");
        line.Provenance.Confidence.ShouldBe(DataSourceConfidence.HtmlScraper);
        line.Provenance.RawRecordHash.Length.ShouldBe(64);
        (await players.GetAsync(
            line.PlayerId,
            TestContext.Current.CancellationToken))!
            .FullName.ShouldBe("Nikola Jokic");
    }

    public void Dispose() => client.Dispose();

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.ShouldBe(DataSourceName.BasketballReference);
            return client;
        }
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var fixtureName = request.RequestUri?.AbsolutePath switch
            {
                "/leagues/NBA_2026_per_game.html" =>
                    "basketball-reference-per-game-2026-07-29.html",
                "/leagues/NBA_2026_totals.html" =>
                    "basketball-reference-totals-2026-07-29.html",
                "/leagues/NBA_2026_advanced.html" =>
                    "basketball-reference-advanced-2026-07-29.html",
                _ => throw new InvalidOperationException(
                    $"No fixture exists for {request.RequestUri}."),
            };
            var fixturePath = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Html",
                fixtureName);
            var html = await File.ReadAllTextAsync(fixturePath, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html),
                RequestMessage = request,
            };
        }
    }

    private sealed class FakePlayerRepository : IPlayerRepository
    {
        private readonly List<Player> players = [];
        private readonly List<ExternalPlayerIdentity> identities = [];

        public Task AddAsync(Player player, CancellationToken cancellationToken)
        {
            players.Add(player);
            return Task.CompletedTask;
        }

        public Task<Player?> GetAsync(
            PlayerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult(players.SingleOrDefault(player => player.Id == id));

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken)
        {
            var identity = identities.SingleOrDefault(candidate =>
                candidate.Provider == provider
                && candidate.ExternalId == externalId);
            return Task.FromResult(identity is null
                ? null
                : players.Single(player => player.Id == identity.PlayerId));
        }

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>(players
                .Where(player => player.NormalizedName == normalizedName)
                .ToArray());

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            Task.FromResult(identities.SingleOrDefault(identity =>
                identity.PlayerId == playerId && identity.Provider == provider));

        public Task AddResolvedIdentityAsync(
            Player player,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken)
        {
            if (addPlayer)
            {
                players.Add(player);
            }

            identities.Add(identity);
            return Task.CompletedTask;
        }

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"Unexpected pending identity for {pendingMatch.ExternalId}.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
