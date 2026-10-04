using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed partial class ApiHttpTests
{
    [Fact]
    public async Task P13_HTTP_recalculation_ranks_then_pick_and_undo_restore_the_board()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var first = await AddPlayerAsync("First projection fixture", token);
        var second = await AddPlayerAsync("Second projection fixture", token);
        await SeedProjectionAsync(first, league, token);
        await SeedProjectionAsync(second, league, token);
        using var pools = await client.GetAsync($"/api/leagues/{league}/projection-pools", token);
        pools.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var calculation = await client.PostAsJsonAsync($"/api/leagues/{league}/projections",
            new { SeasonEndYear = 2026, Source = DataSourceName.Manual }, token);
        calculation.StatusCode.ShouldBe(HttpStatusCode.OK);
        var publication = await ReadEnvelopeAsync(calculation, token);
        publication.RootElement.GetProperty("data").GetProperty("playerCount").GetInt32().ShouldBe(2);
        var draft = await CreateDraftAsync(league, token);
        async Task<string> Board()
        {
            using var response = await client.GetAsync($"/api/drafts/{draft}/board?leagueId={league}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await ReadEnvelopeAsync(response, token)).RootElement.GetProperty("data").GetRawText();
        }
        var before = await Board();
        JsonNode.Parse(before)!["rankings"]!.AsArray().Count.ShouldBe(2);
        await RecordPickAsync(draft, 1, first, HttpStatusCode.OK, token);
        var after = await Board();
        after.ShouldNotContain(first.Value.ToString());
        JsonNode.Parse(after)!["rankings"]!.AsArray().Count.ShouldBe(1);
        using var advice = await client.GetAsync($"/api/drafts/{draft}/recommendations", token);
        advice.StatusCode.ShouldBe(HttpStatusCode.OK);
        var item = (await ReadEnvelopeAsync(advice, token)).RootElement.GetProperty("data")[0];
        item.GetProperty("subjectPlayerId").GetProperty("value").GetGuid().ShouldBe(second.Value);
        item.GetProperty("evidence").GetArrayLength().ShouldBeGreaterThan(0);
        using var undo = await client.DeleteAsync($"/api/drafts/{draft}/picks/1", token);
        undo.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Board()).ShouldBe(before);
        using var scoring = await client.PutAsJsonAsync($"/api/leagues/{league}/scoring",
            new { ScoringRules = new[] { new { Stat = nameof(StatKey.PTS), PointsPerUnit = 2m } } }, token);
        scoring.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rescored = await Board();
        rescored.ShouldNotBe(before, "a scoring change republishes under the new rules");
        JsonNode.Parse(rescored)!["rankings"]!.AsArray().ShouldNotBeEmpty();
        using var missing = await client.PostAsJsonAsync($"/api/leagues/{league}/projections",
            new { SeasonEndYear = 2024, Source = DataSourceName.Manual }, token);
        missing.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var invalid = await client.PostAsJsonAsync($"/api/leagues/{league}/projections",
            new { SeasonEndYear = 1900, Source = "unknown-source" }, token);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task P16_a_new_points_league_is_ranked_without_a_manual_publication()
    {
        var token = TestContext.Current.CancellationToken;
        var seeded = await CreateLeagueAsync(token);
        var player = await AddPlayerAsync("Auto publication fixture", token);
        await SeedProjectionAsync(player, seeded, token);

        var league = await CreateLeagueAsync(token);
        var draft = await CreateDraftAsync(league, token);

        using var response = await client.GetAsync($"/api/drafts/{draft}/board?leagueId={league}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var board = (await ReadEnvelopeAsync(response, token)).RootElement.GetProperty("data").GetRawText();
        board.ShouldContain(player.Value.ToString());
        using var latest = await client.PostAsJsonAsync($"/api/leagues/{league}/projections", new { }, token);
        latest.StatusCode.ShouldBe(HttpStatusCode.OK, "an empty request republishes from the newest imported season");
    }
}
