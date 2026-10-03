using System.Net;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed partial class ApiHttpTests
{
    [Fact]
    public async Task HL03_heat_labels_need_a_model_then_return_the_disclaimer_and_counts()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var guard = await AddPlayerAsync("Fixture Guard", token);
        var center = await AddPlayerAsync("Fixture Center", token);
        await using var scope = app.Services.CreateAsyncScope();
        await RecordedGameFixture.SeedAsync(
            scope.ServiceProvider.GetRequiredService<FantasyDbContext>(), guard.Value, center.Value, null, token);
        var query = $"/api/leagues/{league}/heat-labels?seasonEndYear=2026&source={DataSourceName.Manual}&throughDate=2026-01-14";

        using (var missing = await client.GetAsync(query, token))
        {
            missing.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        var registry = scope.ServiceProvider.GetRequiredService<IModelVersionRepository>();
        await registry.AddAsync(new ModelVersion(
            "heat-prior", "heat-prior-http", DateTimeOffset.UnixEpoch, [2026],
            """{"recentGames":5,"minBaselineGames":10,"maxBaselineGames":30,"minBaselineMinutes":15,"nu":26,"tauRelative":0.18,"effectFloorPoints":2,"effectFloorSd":0,"cvByMinutes":[{"minutesFrom":0,"minutesTo":60,"cv":0.4}]}""",
            """{"falseLabels":{"nullLabelRate":0.05}}""", string.Empty), token);
        await registry.ActivateAsync("heat-prior", "heat-prior-http", token);

        using var response = await client.GetAsync(query, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        using var body = await ReadEnvelopeAsync(response, token);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("modelVersion").GetString().ShouldBe("heat-prior-http");
        data.GetProperty("disclaimer").GetString()!.ShouldContain("Not a forecast");
        data.GetProperty("nullLabelRate").GetDecimal().ShouldBe(0.05m);
        data.GetProperty("labels").GetArrayLength().ShouldBe(0); // 14 fixture games are under the 15-game floor.

        using var invalid = await client.GetAsync(query.Replace("2026-01-14", "bad"), token);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
