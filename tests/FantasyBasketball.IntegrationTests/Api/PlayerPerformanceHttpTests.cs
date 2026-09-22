using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed partial class ApiHttpTests
{
    [Fact]
    public async Task HP01_HTTP_uses_stored_appearances_and_current_scoring_for_distinct_rankings()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var guard = await AddPlayerAsync("Fixture Guard", token);
        var center = await AddPlayerAsync("Fixture Center", token);
        var rookie = await AddPlayerAsync("Fixture Rookie", token);
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FantasyDbContext>();
        await RecordedGameFixture.SeedAsync(database, guard.Value, center.Value, rookie.Value, token);
        var best = await Performance(league, "best");
        best.GetProperty("players")[0].GetProperty("playerId").GetProperty("value").GetGuid().ShouldBe(center.Value);
        best.GetProperty("players")[0].GetProperty("currentAverage").GetDecimal().ShouldBe(30m);
        best.GetProperty("observedPlayers").GetInt32().ShouldBe(3);
        best.GetProperty("bestQualifiedPlayers").GetInt32().ShouldBe(2);
        best.GetProperty("comparisonQualifiedPlayers").GetInt32().ShouldBe(2);
        var hot = await Performance(league, "hot");
        hot.GetProperty("players").GetArrayLength().ShouldBe(1);
        var row = hot.GetProperty("players")[0];
        row.GetProperty("playerId").GetProperty("value").GetGuid().ShouldBe(guard.Value);
        row.GetProperty("seasonAppearances").GetInt32().ShouldBe(13); // Day 5 was DNP.
        row.GetProperty("baselineAverage").GetDecimal().ShouldBe(10m);
        row.GetProperty("recentAverage").GetDecimal().ShouldBe(20m);
        row.GetProperty("pointsAboveBaseline").GetDecimal().ShouldBe(10m);
        row.GetProperty("currentAverage").GetDecimal().ShouldBe(160m / 13m);
        row.GetProperty("baselineWindow").GetArrayLength().ShouldBe(10);
        row.GetProperty("recentWindow").GetArrayLength().ShouldBe(3);
        var baselineIds = row.GetProperty("baselineWindow").EnumerateArray().Select(game => game.GetProperty("gameId").GetGuid());
        baselineIds.Intersect(row.GetProperty("recentWindow").EnumerateArray().Select(game => game.GetProperty("gameId").GetGuid())).ShouldBeEmpty();
        row.GetProperty("recentWindow")[0].GetProperty("provenance").GetProperty("source").GetString().ShouldBe(DataSourceName.Manual);
        hot.GetProperty("latestAppearance").GetString().ShouldBe("2026-01-14");
        hot.GetProperty("latestFetchedAt").GetDateTimeOffset().ShouldBe(new DateTimeOffset(2026, 1, 14, 23, 0, 0, TimeSpan.Zero));
        using var pools = await client.GetAsync($"/api/leagues/{league}/performance-pools", token);
        pools.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var poolBody = await ReadEnvelopeAsync(pools, token);
        poolBody.RootElement.GetProperty("data")[0].GetProperty("gameCount").GetInt32().ShouldBe(14);
        using var changed = await client.PutAsJsonAsync($"/api/leagues/{league}/scoring",
            new { ScoringRules = new[] { new { Stat = nameof(StatKey.AST), PointsPerUnit = 100m } } }, token);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newBest = await Performance(league, "best");
        newBest.GetProperty("players")[0].GetProperty("playerId").GetProperty("value").GetGuid().ShouldBe(guard.Value);
        newBest.GetProperty("players")[0].GetProperty("currentAverage").GetDecimal().ShouldBe(200m);
        newBest.GetProperty("scoringRules")[0].GetProperty("stat").GetString().ShouldBe(nameof(StatKey.AST));
        newBest.GetProperty("scoringRules")[0].GetProperty("pointsPerUnit").GetDecimal().ShouldBe(100m);
        newBest.GetProperty("players")[1].GetProperty("currentAverage").GetDecimal().ShouldBe(0m);
        (await Performance(league, "hot")).GetProperty("players").GetArrayLength().ShouldBe(0);
        (await database.BoxScoreSnapshots.CountAsync(token)).ShouldBe(14);
        (await database.BaselineProjections.CountAsync(token)).ShouldBe(0); // GETs publish no projections.
    }

    [Fact]
    public async Task HP02_empty_and_insufficient_history_remain_explicit_with_date_and_source_isolation()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var empty = await Performance(league, "all");
        empty.GetProperty("observedPlayers").GetInt32().ShouldBe(0);
        empty.GetProperty("latestAppearance").ValueKind.ShouldBe(JsonValueKind.Null);
        empty.GetProperty("latestFetchedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        var guard = await AddPlayerAsync("Fixture Guard", token);
        var center = await AddPlayerAsync("Fixture Center", token);
        await using var scope = app.Services.CreateAsyncScope();
        await RecordedGameFixture.SeedAsync(scope.ServiceProvider.GetRequiredService<FantasyDbContext>(), guard.Value, center.Value, null, token);
        var early = await Performance(league, "all", "2026-01-04");
        foreach (var row in early.GetProperty("players").EnumerateArray())
        {
            row.GetProperty("seasonAppearances").GetInt32().ShouldBe(4);
            row.GetProperty("currentAverage").ValueKind.ShouldBe(JsonValueKind.Null);
            row.GetProperty("pointsAboveBaseline").ValueKind.ShouldBe(JsonValueKind.Null);
        }
        (await Performance(league, "best", "2026-01-04")).GetProperty("players").GetArrayLength().ShouldBe(0);
        (await Performance(league, "all", "2025-12-31")).GetProperty("observedPlayers").GetInt32().ShouldBe(0);
        (await Performance(league, "all", source: DataSourceName.BasketballReference)).GetProperty("observedPlayers").GetInt32().ShouldBe(0);
        (await Performance(league, "all", season: 2025)).GetProperty("observedPlayers").GetInt32().ShouldBe(0);
        (await Performance(league, "all", "2026-01-05")).GetProperty("players")[0].GetProperty("seasonAppearances").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task HP03_validation_category_conflicts_and_ranked_pagination_are_explicit()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var valid = $"seasonEndYear=2026&source={DataSourceName.Manual}&throughDate=2026-01-14&view=best";
        foreach (var query in new[] { "", valid.Replace("2026&", "1900&"), valid.Replace("manual", "unknown"),
            valid.Replace("2026-01-14", "2100-01-01"), valid.Replace("2026-01-14", "invalid"),
            valid.Replace("view=best", "view=unknown"), valid + "&page=0", valid + "&limit=201" })
        {
            using var response = await client.GetAsync($"/api/leagues/{league}/performance?{query}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            using var body = await ReadEnvelopeAsync(response, token);
            body.RootElement.GetProperty("error").GetProperty("code").GetString().ShouldBe("validation_failed");
        }
        var guard = await AddPlayerAsync("Fixture Guard", token);
        var center = await AddPlayerAsync("Fixture Center", token);
        await using var scope = app.Services.CreateAsyncScope();
        await RecordedGameFixture.SeedAsync(scope.ServiceProvider.GetRequiredService<FantasyDbContext>(), guard.Value, center.Value, null, token);
        using var page = await client.GetAsync($"/api/leagues/{league}/performance?{valid}&page=2&limit=1", token);
        using var paged = await ReadEnvelopeAsync(page, token);
        paged.RootElement.GetProperty("meta").GetProperty("total").GetInt32().ShouldBe(2);
        paged.RootElement.GetProperty("data").GetProperty("players")[0].GetProperty("playerId").GetProperty("value").GetGuid().ShouldBe(guard.Value);
        using var category = await client.PostAsJsonAsync("/api/leagues", new
        {
            Name = "Category fixture",
            Type = "Categories",
            TeamCount = 7,
            Categories = new[] { nameof(StatKey.PTS) },
            RosterSlots = new[] { "UTIL" },
            Cadence = "Daily",
        }, token);
        using var categoryBody = await ReadEnvelopeAsync(category, token);
        var categoryId = categoryBody.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        foreach (var path in new[] { "performance-pools", $"performance?{valid}" })
        {
            using var response = await client.GetAsync($"/api/leagues/{categoryId}/{path}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }
    }

    private async Task<JsonElement> Performance(Guid league, string view, string date = "2026-01-14",
        string? source = null, int season = 2026)
    {
        using var response = await client.GetAsync($"/api/leagues/{league}/performance?seasonEndYear={season}&source={source ?? DataSourceName.Manual}&throughDate={date}&view={view}", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        using var body = await ReadEnvelopeAsync(response, TestContext.Current.CancellationToken);
        return body.RootElement.GetProperty("data").Clone();
    }
}
