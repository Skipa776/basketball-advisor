using System.Net;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Providers.Sleeper;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Providers;

/// <summary>
/// Sleeper NBA endpoints validated 2026-09-24 against a real 10-team NBA league; the fixtures are
/// that league's responses with names and ids anonymized, and the player map cut to rostered players.
/// </summary>
public sealed class SleeperLeagueProviderTests
{
    private const string LeagueId = "1000000000000000001";

    [Fact]
    public void Only_the_four_validated_endpoints_can_be_built()
    {
        SleeperUrlBuilder.League(LeagueId).ShouldBe($"/v1/league/{LeagueId}");
        SleeperUrlBuilder.Rosters(LeagueId).ShouldBe($"/v1/league/{LeagueId}/rosters");
        foreach (var path in new[] { $"/v1/league/{LeagueId}/drafts", $"/v1/league/{LeagueId}/transactions/1", "/v1/players/nfl", "/v1/user/someone", "/v1/league/abc" })
        {
            Should.Throw<InvalidOperationException>(() => SleeperUrlBuilder.Build(path));
        }

        SleeperUrlBuilder.IsLeagueId("1408892125055086592").ShouldBeTrue();
        SleeperUrlBuilder.IsLeagueId("../users").ShouldBeFalse();
    }

    [Fact]
    public async Task Real_nba_league_maps_to_the_provider_neutral_snapshot()
    {
        var handler = new FixtureHandler();
        var snapshot = await Provider(handler).GetLeagueAsync(LeagueId, TestContext.Current.CancellationToken);

        snapshot.Provider.ShouldBe(DataSourceName.Sleeper);
        snapshot.TeamCount.ShouldBe(10);
        snapshot.Teams.Count.ShouldBe(10);
        snapshot.Teams.ShouldAllBe(team => team.Players.Count == 15);
        snapshot.Teams[0].Name.ShouldBe("Fixture Owner", "the owned roster takes its owner's name");
        snapshot.Teams[1].Name.ShouldBe("Team 2", "unclaimed rosters are named by roster id");
        snapshot.RosterSlots.ShouldBe(["PG", "SG", "G", "SF", "PF", "F", "C", "UTIL", "UTIL", "BN", "BN", "BN", "BN", "BN", "BN"]);
        snapshot.Scoring["pts"].ShouldBe(0.5m);
        snapshot.Scoring.ShouldContainKey("dd");
        var george = snapshot.Teams.SelectMany(team => team.Players).Single(player => player.FullName == "Paul George");
        george.Positions.ShouldBe(["PF", "SF", "SG"], "Sleeper's multi-position eligibility");
        george.Provenance.Source.ShouldBe(DataSourceName.Sleeper);
        george.Provenance.ExternalId.ShouldBe(george.ExternalPlayerId);

        await Provider(handler).GetLeagueAsync(LeagueId, TestContext.Current.CancellationToken);
        handler.Requests.Count(path => path == "/v1/players/nba").ShouldBe(2, "each provider instance shown here has its own cache");
    }

    [Fact]
    public async Task The_player_map_is_fetched_once_per_cache_and_unknown_leagues_are_named()
    {
        var handler = new FixtureHandler();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = Provider(handler, cache);
        await provider.GetLeagueAsync(LeagueId, TestContext.Current.CancellationToken);
        await provider.GetLeagueAsync(LeagueId, TestContext.Current.CancellationToken);
        handler.Requests.Count(path => path == "/v1/players/nba").ShouldBe(1, "Sleeper asks for the player map at most daily");

        (await Should.ThrowAsync<KeyNotFoundException>(() => provider.GetLeagueAsync("42", TestContext.Current.CancellationToken)))
            .Message.ShouldContain("no league");
        await Should.ThrowAsync<ArgumentException>(() => provider.GetLeagueAsync("not-a-number", TestContext.Current.CancellationToken));
    }

    private static SleeperLeagueProvider Provider(FixtureHandler handler, IMemoryCache? cache = null) =>
        new(new SingleClientFactory(new HttpClient(handler) { BaseAddress = new Uri("https://api.sleeper.app") }),
            cache ?? new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);

    private sealed class FixtureHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(path);
            var fixture = path switch
            {
                $"/v1/league/{LeagueId}" => "sleeper-league-2026-09-24.json",
                $"/v1/league/{LeagueId}/users" => "sleeper-users-2026-09-24.json",
                $"/v1/league/{LeagueId}/rosters" => "sleeper-rosters-2026-09-24.json",
                "/v1/players/nba" => "sleeper-players-nba-2026-09-24.json",
                _ => null,
            };
            var body = fixture is null
                ? "null"
                : await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Json", fixture), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
