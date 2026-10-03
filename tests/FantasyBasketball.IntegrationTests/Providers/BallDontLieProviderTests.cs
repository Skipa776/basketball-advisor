using System.Net;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Providers.BallDontLie;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Providers;

public sealed class BallDontLieProviderTests : IDisposable
{
    private readonly HttpClient client;
    private readonly BallDontLieProvider provider;
    private readonly NbaTeam atlanta;
    private readonly NbaTeam boston;

    public BallDontLieProviderTests()
    {
        atlanta = new NbaTeam(new NbaTeamId(Guid.NewGuid()), "Atlanta Hawks", "ATL");
        boston = new NbaTeam(new NbaTeamId(Guid.NewGuid()), "Boston Celtics", "BOS");
        client = new HttpClient(new FixtureHandler())
        {
            BaseAddress = new Uri("https://api.balldontlie.io"),
        };
        provider = new BallDontLieProvider(
            new SingleClientFactory(client),
            new FakeTeamRepository(atlanta, boston),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task I01_teams_payload_maps_to_external_teams()
    {
        var teams = await provider.GetTeamsAsync(TestContext.Current.CancellationToken);

        teams.Count.ShouldBe(2);
        teams[0].ExternalId.ShouldBe("1");
        teams[0].Name.ShouldBe("Atlanta Hawks");
        teams[0].Abbreviation.ShouldBe("ATL");
        teams[0].Provenance.Source.ShouldBe(DataSourceName.BallDontLie);
    }

    [Fact]
    public async Task I02_players_payload_preserves_external_id_and_resolves_team()
    {
        var players = await provider.GetPlayersAsync(TestContext.Current.CancellationToken);

        players.Count.ShouldBe(3);
        players[0].ExternalId.ShouldBe("15");
        players[0].FullName.ShouldBe("Nikola Jokic");
        players[0].TeamId.ShouldBe(atlanta.Id);
        players[0].Positions.ShouldBe(["C"]);
        players[1].Positions.ShouldBe(["G", "F"]);
        players[2].ExternalId.ShouldBe("23");
        players.All(player =>
            player.Provenance.RawRecordHash.Length == 64).ShouldBeTrue();
    }

    [Fact]
    public async Task I17_nameless_placeholder_player_is_skipped()
    {
        // Page 2 ends with a blank-name record copied from the live directory.
        var players = await provider.GetPlayersAsync(TestContext.Current.CancellationToken);

        players.ShouldNotContain(player => player.ExternalId == "1093928141");
        players.Count.ShouldBe(3);
    }

    [Fact]
    public async Task I03_games_payload_maps_teams_and_utc_timestamp()
    {
        var games = await provider.GetGamesAsync(
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            TestContext.Current.CancellationToken);

        games.Count.ShouldBe(1);
        games[0].HomeTeamId.ShouldBe(atlanta.Id);
        games[0].AwayTeamId.ShouldBe(boston.Id);
        games[0].StartsAt.Offset.ShouldBe(TimeSpan.Zero);
        games[0].SeasonEndYear.ShouldBe(2026);
        games[0].Provenance.ExternalId.ShouldBe("9001");
    }

    [Fact]
    public async Task I14_game_without_datetime_keeps_its_eastern_date()
    {
        // balldontlie sends datetime null for all 11 games on 2022-12-02; the run used to fail.
        var games = await provider.GetGamesAsync(
            new DateOnly(2022, 12, 2),
            new DateOnly(2022, 12, 2),
            TestContext.Current.CancellationToken);

        var game = games.ShouldHaveSingleItem();
        game.StartsAt.ShouldBe(new DateTimeOffset(2022, 12, 2, 17, 0, 0, TimeSpan.Zero)); // noon EST
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
                game.StartsAt, TimeZoneInfo.FindSystemTimeZoneById("America/New_York")).DateTime)
            .ShouldBe(new DateOnly(2022, 12, 2));
        game.SeasonEndYear.ShouldBe(2023);
    }

    [Fact]
    public async Task I10_unchanged_payload_produces_stable_hash()
    {
        var first = await provider.GetTeamsAsync(TestContext.Current.CancellationToken);
        var second = await provider.GetTeamsAsync(TestContext.Current.CancellationToken);

        first[0].Provenance.RawRecordHash.ShouldBe(
            second[0].Provenance.RawRecordHash);
    }

    public void Dispose() => client.Dispose();

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            name.ShouldBe(DataSourceName.BallDontLie);
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
                "/v1/teams" => "balldontlie-teams-2026-07-29.json",
                "/v1/players" when request.RequestUri.Query.Contains(
                    "cursor=page-2",
                    StringComparison.Ordinal) =>
                    "balldontlie-players-page2-2026-07-29.json",
                "/v1/players" => "balldontlie-players-2026-07-29.json",
                "/v1/games" when request.RequestUri.Query.Contains(
                    "start_date=2022-12-02",
                    StringComparison.Ordinal) =>
                    "balldontlie-games-missing-datetime-2022-12-02.json",
                "/v1/games" => "balldontlie-games-2026-07-29.json",
                _ => throw new InvalidOperationException(
                    $"No fixture exists for {request.RequestUri}."),
            };
            var fixturePath = Path.Combine(
                AppContext.BaseDirectory,
                "Fixtures",
                "Json",
                fixtureName);
            var json = await File.ReadAllTextAsync(fixturePath, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
                RequestMessage = request,
            };
        }
    }

    private sealed class FakeTeamRepository(params NbaTeam[] teams) : ITeamRepository
    {
        public Task AddAsync(
            NbaTeam team,
            DataProvenance provenance,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<NbaTeam?> FindByAbbreviationAsync(
            string abbreviation,
            CancellationToken cancellationToken) =>
            Task.FromResult(teams.SingleOrDefault(team =>
                team.Abbreviation == abbreviation));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
