using System.Net;
using FantasyBasketball.Infrastructure.Providers.Sleeper;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Providers;

/// <summary>
/// The user-drafts fixture is synthetic in the documented response shape (docs.sleeper.com,
/// 2026-10-02); the picks are the anonymized real 150-pick draft from the league fixtures.
/// </summary>
public sealed class SleeperDraftLogImporterTests
{
    private const string LeagueId = "1000000000000000001";

    [Fact]
    public async Task DL03_seed_resolves_a_league_to_its_members_and_a_name_to_one_user()
    {
        var importer = Importer();

        (await importer.SeedUsersAsync(LeagueId, TestContext.Current.CancellationToken)).ShouldBe(["100"]);
        (await importer.SeedUsersAsync("fixture_owner", TestContext.Current.CancellationToken)).ShouldBe(["100"]);
        await Should.ThrowAsync<KeyNotFoundException>(() => importer.SeedUsersAsync("nobody", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DL04_user_drafts_and_picks_map_to_draft_logs()
    {
        var importer = Importer();

        var drafts = await importer.ListUserDraftsAsync("100", 2025, TestContext.Current.CancellationToken);
        var log = await importer.GetDraftAsync(drafts[0], TestContext.Current.CancellationToken);

        drafts.Select(draft => (draft.Status, draft.Type)).ShouldBe([("complete", "snake"), ("complete", "auction"), ("drafting", "snake")]);
        drafts[0].Slots["UTIL"].ShouldBe(2);
        drafts[0].ScoringType.ShouldBe("points");
        log.Picks.Count.ShouldBe(150);
        log.Picks[0].PlayerName.ShouldBe("Nikola Jokić");
        log.Picks[0].Positions.ShouldBe(["C"]);
        log.Picks[1].PickedBy.ShouldBeNull("an empty picked_by is an autopick, not a user");
    }

    private static SleeperDraftLogImporter Importer() =>
        new(new SingleClientFactory(new HttpClient(new FixtureHandler()) { BaseAddress = new Uri("https://api.sleeper.app") }));

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath switch
            {
                $"/v1/league/{LeagueId}/users" => await Fixture("sleeper-users-2026-09-24.json", cancellationToken),
                "/v1/user/fixture_owner" => """{"user_id":"100","username":"fixture_owner"}""",
                "/v1/user/100/drafts/nba/2025" => await Fixture("sleeper-user-drafts-2026-10-02.json", cancellationToken),
                "/v1/draft/2000000000000000001/picks" => await Fixture("sleeper-draft-picks-2026-09-26.json", cancellationToken),
                _ => "null",
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }

        private static Task<string> Fixture(string name, CancellationToken cancellationToken) =>
            File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Json", name), cancellationToken);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
