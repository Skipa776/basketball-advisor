using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FantasyBasketball.Api;
using FantasyBasketball.Api.Endpoints;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using FantasyBasketball.Infrastructure.Workers;
using FantasyBasketball.IntegrationTests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed partial class ApiHttpTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();
    private WebApplication app = null!;
    private HttpClient client = null!;
    private readonly Guid ownerId = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ApiHost).Assembly.GetName().Name,
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "fixture-api-key",
                ["ConnectionStrings:Fantasy"] = postgres.GetConnectionString(),
            });
        ApiHost.ConfigureServices(builder);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.RemoveAll<IUserContext>();
        builder.Services.AddSingleton<IUserContext>(
            new FixedUserContext(ownerId));
        builder.Services.RemoveAll<IImportJobQueue>();
        builder.Services.RemoveAll<ImportJobQueue>();
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton<IImportJobQueue, FakeImportJobQueue>();
        app = builder.Build();
        ApiHost.Configure(app);
        app.MapGet("/api/test/throw", ThrowEndpoint);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<FantasyDbContext>();
            await database.Database.MigrateAsync(
                TestContext.Current.CancellationToken);
            database.Users.Add(new FantasyUser
            {
                Id = ownerId,
                UserName = "api-test",
                NormalizedUserName = "API-TEST",
                DisplayName = "API Test",
                CreatedAt = DateTimeOffset.UnixEpoch,
                IsInstanceOwner = true,
            });
            await database.SaveChangesAsync(
                TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException(
                "Kestrel did not publish an address.");
        client = new HttpClient
        {
            BaseAddress = new Uri(addresses.Single()),
        };
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.StopAsync(TestContext.Current.CancellationToken);
        await app.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task League_setup_rejects_undefined_numeric_enum_values()
    {
        var token = TestContext.Current.CancellationToken;
        foreach (var field in new[] { "Type", "Cadence", "ScoringRules", "Categories", "RosterSlots" })
        {
            var payload = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(
                LeagueRequest([new { Stat = nameof(StatKey.PTS), PointsPerUnit = 1m }])))!;
            if (field == "ScoringRules") payload[field]![0]!["Stat"] = "999";
            else if (field is "Categories" or "RosterSlots") payload[field] = new System.Text.Json.Nodes.JsonArray("999");
            else payload[field] = "999";
            using var response = await client.PostAsJsonAsync("/api/leagues", payload, token);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, field);
        }
    }

    [Fact]
    public async Task React_setup_leagues_and_draft_restore_use_persisted_state()
    {
        var token = TestContext.Current.CancellationToken;
        using var setupResponse = await client.GetAsync("/api/leagues/setup", token);
        using var setup = await ReadEnvelopeAsync(setupResponse, token);
        var catalog = setup.RootElement.GetProperty("data");
        catalog.GetProperty("suggestedTeamCount").GetInt32().ShouldBe(7);
        var rules = catalog.GetProperty("pointsProfile").GetProperty("rules");
        rules.GetArrayLength().ShouldBe(11);
        // Golden independent of the setup implementation: ESPN's 25-point,
        // 8-rebound, 6-assist, 2-steal, 1-block, 3-turnover shooting line = 53.
        var stats = new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 25,
            [StatKey.REB] = 8,
            [StatKey.AST] = 6,
            [StatKey.STL] = 2,
            [StatKey.BLK] = 1,
            [StatKey.TOV] = 3,
            [StatKey.FGM] = 9,
            [StatKey.FGA] = 18,
            [StatKey.FG3M] = 3,
            [StatKey.FTM] = 4,
            [StatKey.FTA] = 5,
        };
        rules.EnumerateArray().Sum(rule => stats[Enum.Parse<StatKey>(rule.GetProperty("stat").GetString()!)]
            * rule.GetProperty("pointsPerUnit").GetDecimal()).ShouldBe(53m);
        using var create = await client.PostAsJsonAsync("/api/leagues", new
        {
            Name = "Eleven team preparation",
            Type = "Points",
            TeamCount = 11,
            ScoringRules = rules,
            Categories = Array.Empty<string>(),
            RosterSlots = new[] { "PG", "SG", "UTIL" },
            Cadence = "Daily",
        }, token);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadEnvelopeAsync(create, token);
        var league = created.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        using var listResponse = await client.GetAsync("/api/leagues", token);
        using var list = await ReadEnvelopeAsync(listResponse, token);
        list.RootElement.GetProperty("meta").GetProperty("limit").GetInt32().ShouldBe(50);
        list.RootElement.GetProperty("data")[0].GetProperty("id").GetGuid().ShouldBe(league);
        using var invalid = await client.GetAsync("/api/leagues?page=0", token);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var draft = await CreateDraftAsync(league, token);
        var player = await AddPlayerAsync("Persisted pick", token);
        await RecordPickAsync(draft, 1, player, HttpStatusCode.OK, token);
        using var restoredResponse = await client.GetAsync($"/api/drafts/{draft}", token);
        using var restored = await ReadEnvelopeAsync(restoredResponse, token);
        var record = restored.RootElement.GetProperty("data");
        record.GetProperty("leagueId").GetGuid().ShouldBe(league);
        var session = record.GetProperty("session");
        session.GetProperty("teamCount").GetInt32().ShouldBe(11);
        session.GetProperty("currentPick").GetInt32().ShouldBe(2);
        session.GetProperty("picks")[0].GetProperty("playerId").GetProperty("value").GetGuid().ShouldBe(player.Value);
    }

    [Fact]
    public async Task Saved_drafts_are_paged_and_league_settings_refuse_structural_edits_after_draft_creation()
    {
        var token = TestContext.Current.CancellationToken;
        var league = await CreateLeagueAsync(token);
        var firstDraft = await CreateDraftAsync(league, token);
        var secondDraft = await CreateDraftAsync(league, token);

        using var list = await client.GetAsync(
            $"/api/drafts?leagueId={league}&page=1&limit=1",
            token);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var page = await ReadEnvelopeAsync(list, token);
        page.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1);
        page.RootElement.GetProperty("meta").GetProperty("total").GetInt32().ShouldBe(2);

        using var structuralEdit = await client.PutAsJsonAsync(
            $"/api/leagues/{league}/settings",
            new { Name = "Renamed league", TeamCount = 10, Cadence = "Weekly", RosterSlots = new[] { "PG", "BENCH" } },
            token);
        structuralEdit.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var presentationEdit = await client.PutAsJsonAsync(
            $"/api/leagues/{league}/settings",
            new { Name = "Renamed league", TeamCount = 10, Cadence = "Weekly", RosterSlots = new[] { "UTIL" } },
            token);
        presentationEdit.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var saved = await ReadEnvelopeAsync(presentationEdit, token);
        saved.RootElement.GetProperty("data").GetProperty("name").GetString().ShouldBe("Renamed league");
        saved.RootElement.GetProperty("data").GetProperty("cadence").GetInt32()
            .ShouldBe((int)LineupCadence.Weekly);
        var draft = await CreateDraftAsync(league, token);
        draft.ShouldNotBe(firstDraft);
        draft.ShouldNotBe(secondDraft);

        using var reorderedLeagueResponse = await client.PostAsJsonAsync("/api/leagues", new
        {
            Name = "Ordered roster",
            Type = "Points",
            TeamCount = 10,
            ScoringRules = new[] { new { Stat = nameof(StatKey.PTS), PointsPerUnit = 1m } },
            Categories = Array.Empty<string>(),
            RosterSlots = new[] { "SG", "PG" },
            Cadence = "Daily",
        }, token);
        reorderedLeagueResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var reorderedLeague = await ReadEnvelopeAsync(reorderedLeagueResponse, token);
        var reorderedLeagueId = reorderedLeague.RootElement.GetProperty("data").GetProperty("id").GetGuid();
        await CreateDraftAsync(reorderedLeagueId, token);
        using var sameSlots = await client.PutAsJsonAsync(
            $"/api/leagues/{reorderedLeagueId}/settings",
            new { Name = "Renamed ordered roster", TeamCount = 10, Cadence = "Weekly", RosterSlots = new[] { "PG", "SG" } },
            token);
        sameSlots.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Projected_players_are_ranked_by_season_value_and_paged()
    {
        var token = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(token);
        await AssertProjectedPlayersAsync(leagueId, 1, 50, [], 0, token);

        var low = await AddPlayerAsync("Low Value Player", token);
        var high = await AddPlayerAsync("High Value Player", token);
        var middle = await AddPlayerAsync("Middle Value Player", token);
        await SeedProjectionAsync(low, leagueId, token, seasonTotal: 300m);
        await SeedProjectionAsync(high, leagueId, token, seasonTotal: 900m);
        await SeedProjectionAsync(middle, leagueId, token, seasonTotal: 600m);

        await AssertProjectedPlayersAsync(
            leagueId, 1, 2, [(1, "High Value Player", 900m), (2, "Middle Value Player", 600m)], 3, token);
        await AssertProjectedPlayersAsync(
            leagueId, 2, 2, [(3, "Low Value Player", 300m)], 3, token);
    }

    [Fact]
    public async Task League_settings_validate_names_and_round_trip_weekly_acquisitions()
    {
        var token = TestContext.Current.CancellationToken;
        object Request(string name) => new
        {
            Name = name,
            Type = "Points",
            TeamCount = 10,
            ScoringRules = new[] { new { Stat = nameof(StatKey.PTS), PointsPerUnit = 1m } },
            Categories = Array.Empty<string>(),
            RosterSlots = new[] { "UTIL" },
            Cadence = "Daily",
        };

        using var tooLong = await client.PostAsJsonAsync("/api/leagues", Request(new string('x', 101)), token);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using (var error = await ReadEnvelopeAsync(tooLong, token))
        {
            error.RootElement.GetProperty("error").GetProperty("fields")
                .TryGetProperty("name", out _).ShouldBeTrue();
        }

        using var atLimit = await client.PostAsJsonAsync("/api/leagues", Request(new string('x', 100)), token);
        atLimit.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var created = await ReadEnvelopeAsync(atLimit, token);
        var leagueId = created.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        using var rename = await client.PutAsJsonAsync(
            $"/api/leagues/{leagueId}/settings",
            new { Name = new string('y', 101), TeamCount = 10, Cadence = "Daily", RosterSlots = new[] { "UTIL" } },
            token);
        rename.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        created.RootElement.GetProperty("data").GetProperty("weeklyAcquisitionLimit").GetInt32().ShouldBe(7, "ESPN's common default");
        using (var limit = await client.PutAsJsonAsync($"/api/leagues/{leagueId}/settings",
            new { Name = "Limited", TeamCount = 10, Cadence = "Daily", RosterSlots = new[] { "UTIL" }, WeeklyAcquisitionLimit = 4 }, token))
        {
            limit.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var reloaded = await client.GetAsync($"/api/leagues/{leagueId}", token))
        using (var body = await ReadEnvelopeAsync(reloaded, token))
        {
            body.RootElement.GetProperty("data").GetProperty("weeklyAcquisitionLimit").GetInt32().ShouldBe(4);
        }

        using var invalid = await client.PutAsJsonAsync($"/api/leagues/{leagueId}/settings",
            new { Name = "Limited", TeamCount = 10, Cadence = "Daily", RosterSlots = new[] { "UTIL" }, WeeklyAcquisitionLimit = 0 }, token);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task AssertProjectedPlayersAsync(
        Guid leagueId,
        int page,
        int limit,
        IReadOnlyList<(int Rank, string Name, decimal Value)> expected,
        int expectedTotal,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"/api/leagues/{leagueId}/projected-players?page={page}&limit={limit}",
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await ReadEnvelopeAsync(response, cancellationToken);
        var rows = document.RootElement.GetProperty("data").EnumerateArray()
            .Select(row => (
                row.GetProperty("rank").GetInt32(),
                row.GetProperty("fullName").GetString()!,
                row.GetProperty("projectedSeasonValue").GetDecimal()))
            .ToArray();
        rows.ShouldBe(expected);
        document.RootElement.GetProperty("meta").GetProperty("total").GetInt32()
            .ShouldBe(expectedTotal);
    }

    [Fact]
    public async Task Category_league_setup_accepts_explicit_categories_without_points_defaults()
    {
        using var response = await client.PostAsJsonAsync("/api/leagues", new
        {
            Name = "Category configuration",
            Type = "Categories",
            TeamCount = 12,
            ScoringRules = Array.Empty<object>(),
            Categories = new[] { "PTS", "REB", "AST" },
            RosterSlots = new[] { "PG", "SG", "UTIL", "BENCH" },
            Cadence = "Weekly",
        }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var document = await ReadEnvelopeAsync(response, TestContext.Current.CancellationToken);
        var data = document.RootElement.GetProperty("data");
        data.GetProperty("type").GetInt32().ShouldBe(1);
        data.GetProperty("scoringRules").GetArrayLength().ShouldBe(0);
        data.GetProperty("categories").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task A10_through_A14_http_contract_and_lifecycles_hold()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using (var invalidLeague = await client.PostAsJsonAsync(
            "/api/leagues",
            LeagueRequest(scoringRules: []),
            cancellationToken))
        {
            invalidLeague.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            var document = await ReadEnvelopeAsync(invalidLeague, cancellationToken);
            document.RootElement.GetProperty("error")
                .GetProperty("fields")
                .TryGetProperty("scoringRules", out _)
                .ShouldBeTrue();
        }

        var leagueId = await CreateLeagueAsync(cancellationToken);
        var firstPlayer = await AddPlayerAsync("First API Player", cancellationToken);
        var secondPlayer = await AddPlayerAsync("Second API Player", cancellationToken);
        await SeedProjectionAsync(
            firstPlayer,
            leagueId,
            cancellationToken);

        await AssertSuccessAsync(
            await client.GetAsync(
                $"/api/leagues/{leagueId}",
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.PutAsJsonAsync(
                $"/api/leagues/{leagueId}/scoring",
                new
                {
                    ScoringRules = new[]
                    {
                        new
                        {
                            Stat = nameof(StatKey.PTS),
                            PointsPerUnit = 1.25m,
                        },
                    },
                },
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.GetAsync(
                "/api/players?search=First&page=1&limit=20",
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.GetAsync(
                $"/api/players/{firstPlayer.Value}",
                cancellationToken),
            cancellationToken);
        using (var republished = await client.GetAsync(
            $"/api/players/{firstPlayer.Value}/projection?leagueId={leagueId}", cancellationToken))
        {
            // The scoring change republished values under the new rules.
            republished.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await AssertSuccessAsync(
            await client.PostAsJsonAsync($"/api/leagues/{leagueId}/projections",
                new { SeasonEndYear = 2026, Source = DataSourceName.Manual }, cancellationToken),
            cancellationToken);
        using (var projection = await client.GetAsync(
            $"/api/players/{firstPlayer.Value}/projection?leagueId={leagueId}",
            cancellationToken))
        {
            projection.StatusCode.ShouldBe(HttpStatusCode.OK);
            var document = await ReadEnvelopeAsync(projection, cancellationToken);
            var data = document.RootElement.GetProperty("data");
            data.TryGetProperty("observed", out _).ShouldBeTrue();
            data.TryGetProperty("baseline", out _).ShouldBeTrue();
            data.TryGetProperty("adjusted", out _).ShouldBeTrue();
            data.TryGetProperty("value", out _).ShouldBeTrue();
        }

        var draftId = await CreateDraftAsync(leagueId, cancellationToken);
        var firstPickId = await RecordPickAsync(
            draftId,
            1,
            firstPlayer,
            HttpStatusCode.OK,
            cancellationToken);
        var repeatedPickId = await RecordPickAsync(
            draftId,
            1,
            firstPlayer,
            HttpStatusCode.OK,
            cancellationToken);
        repeatedPickId.ShouldBe(firstPickId);
        await RecordPickAsync(
            draftId,
            1,
            secondPlayer,
            HttpStatusCode.Conflict,
            cancellationToken);
        await RecordPickAsync(
            draftId,
            2,
            secondPlayer,
            HttpStatusCode.OK,
            cancellationToken);
        await AssertFailureAsync(
            await client.DeleteAsync(
                $"/api/drafts/{draftId}/picks/1",
                cancellationToken),
            HttpStatusCode.Conflict,
            "conflict",
            cancellationToken);
        await AssertSuccessAsync(
            await client.DeleteAsync(
                $"/api/drafts/{draftId}/picks/2",
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.GetAsync(
                $"/api/drafts/{draftId}/board?leagueId={leagueId}",
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.GetAsync(
                $"/api/drafts/{draftId}/recommendations",
                cancellationToken),
            cancellationToken);

        var contextEventId = await CreateContextEventAsync(
            firstPlayer,
            cancellationToken);
        var reviewer = Guid.NewGuid();
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                $"/api/context-events/{contextEventId}/verify",
                new { UserId = reviewer },
                cancellationToken),
            cancellationToken);
        await AssertFailureAsync(
            await client.PostAsJsonAsync(
                $"/api/context-events/{contextEventId}/verify",
                new { UserId = reviewer },
                cancellationToken),
            HttpStatusCode.Conflict,
            "conflict",
            cancellationToken);
        var rejectedEventId = await CreateContextEventAsync(
            secondPlayer,
            cancellationToken);
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                $"/api/context-events/{rejectedEventId}/reject",
                new { UserId = reviewer },
                cancellationToken),
            cancellationToken);
        await AssertFailureAsync(
            await client.PostAsJsonAsync(
                $"/api/context-events/{rejectedEventId}/verify",
                new { UserId = reviewer },
                cancellationToken),
            HttpStatusCode.Conflict,
            "conflict",
            cancellationToken);
        using (var contextList = await client.GetAsync(
            "/api/context-events?page=1&limit=50",
            cancellationToken))
        {
            contextList.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var contextDocument = await ReadEnvelopeAsync(contextList, cancellationToken);
            var verified = contextDocument.RootElement.GetProperty("data")
                .EnumerateArray()
                .Single(value => value.GetProperty("id").GetGuid() == contextEventId);
            verified.GetProperty("type").ValueKind.ShouldBe(JsonValueKind.Number);
            verified.GetProperty("typeName").GetString().ShouldBe("RotationChange");
            verified.GetProperty("verification").ValueKind.ShouldBe(JsonValueKind.Number);
            verified.GetProperty("verificationName").GetString().ShouldBe("Verified");
        }

        await AssertSuccessAsync(
            await client.GetAsync(
                "/api/health/data-sources",
                cancellationToken),
            cancellationToken);
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                "/api/imports/players",
                new { },
                cancellationToken),
            cancellationToken,
            HttpStatusCode.Accepted);
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                "/api/imports/schedule",
                new
                {
                    From = new DateOnly(2026, 10, 1),
                    To = new DateOnly(2026, 10, 2),
                },
                cancellationToken),
            cancellationToken,
            HttpStatusCode.Accepted);
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                "/api/imports/season-stats",
                new { SeasonEndYear = 2026 },
                cancellationToken),
            cancellationToken,
            HttpStatusCode.Accepted);
        await AssertSuccessAsync(
            await client.PostAsJsonAsync(
                "/api/imports/adp",
                new { Csv = (string?)null },
                cancellationToken),
            cancellationToken,
            HttpStatusCode.Accepted);
        await AssertSuccessAsync(
            await client.GetAsync(
                "/api/imports/runs?page=1&limit=50",
                cancellationToken),
            cancellationToken);

        await AssertFailureAsync(
            await client.GetAsync("/api/does-not-exist", cancellationToken),
            HttpStatusCode.NotFound,
            "not_found",
            cancellationToken);
        using (var failure = await client.GetAsync(
            "/api/test/throw",
            cancellationToken))
        {
            failure.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            var document = await ReadEnvelopeAsync(failure, cancellationToken);
            var body = document.RootElement.GetRawText();
            body.ShouldNotContain("fixture-connection-string");
            body.ShouldNotContain("stack");
            body.ShouldNotContain("SQL");
            document.RootElement.GetProperty("error")
                .GetProperty("code")
                .GetString()
                .ShouldBe("internal_error");
        }
    }

    [Fact]
    public async Task A35_list_routes_page_themselves_without_a_query_string()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Each of these answered 400 before the fix: a minimal-API `int page`
        // the caller omits is a binding failure, not a zero. Every existing
        // row passed "?page=1&limit=50", which is why the break survived a
        // green suite -- so the bare path is the only thing asserted here.
        string[] listRoutes =
        [
            "/api/players",
            "/api/context-events",
            "/api/imports/runs",
        ];

        foreach (var route in listRoutes)
        {
            using var response = await client.GetAsync(route, cancellationToken);
            response.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"GET {route} with no query string must apply the documented defaults.");
            using var document = await ReadEnvelopeAsync(response, cancellationToken);
            var meta = document.RootElement.GetProperty("meta");
            meta.GetProperty("page").GetInt32().ShouldBe(1);
            meta.GetProperty("limit").GetInt32().ShouldBe(Paging.DefaultLimit);
        }
    }

    [Fact]
    public async Task League_rosters_import_from_csv_replace_on_reimport_and_report_unmatched_names()
    {
        var token = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(token);
        await AddPlayerAsync("Roster Jokić", token);
        await AddPlayerAsync("Roster Curry", token);
        await AddPlayerAsync("Roster Doncic", token);

        using (var imported = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/teams/csv", new
        {
            Csv = "Team,Player,Mine\nMy Squad,Roster Jokic,yes\nMy Squad,Roster Curry,yes\nRivals,Roster Doncic,\nRivals,Nobody Real,\n",
        }, token))
        {
            imported.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await ReadEnvelopeAsync(imported, token);
            var data = body.RootElement.GetProperty("data");
            data.GetProperty("teams").GetInt32().ShouldBe(2);
            data.GetProperty("players").GetInt32().ShouldBe(3);
            data.GetProperty("unmatchedPlayers")[0].GetString().ShouldBe("Nobody Real (line 5)");
        }

        using (var listed = await client.GetAsync($"/api/leagues/{leagueId}/teams", token))
        {
            using var body = await ReadEnvelopeAsync(listed, token);
            var teams = body.RootElement.GetProperty("data");
            teams.GetArrayLength().ShouldBe(2);
            teams[0].GetProperty("name").GetString().ShouldBe("My Squad");
            teams[0].GetProperty("isUsersTeam").GetBoolean().ShouldBeTrue();
            teams[0].GetProperty("players").EnumerateArray().Select(player => player.GetProperty("name").GetString())
                .ShouldBe(["Roster Jokić", "Roster Curry"]);
            teams[1].GetProperty("isUsersTeam").GetBoolean().ShouldBeFalse();
        }

        using (var replaced = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/teams/csv", new { Csv = "Team,Player\nSolo,Roster Curry" }, token))
        {
            replaced.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var listed = await client.GetAsync($"/api/leagues/{leagueId}/teams", token))
        {
            using var body = await ReadEnvelopeAsync(listed, token);
            body.RootElement.GetProperty("data").GetArrayLength().ShouldBe(1, "an import replaces the league's rosters");
        }

        var tooMany = "Team,Player\n" + string.Join("\n", Enumerable.Range(1, 11).Select(team => $"Team {team},Roster Curry"));
        foreach (var (csv, reason) in new[]
        {
            ("Team,Player\nA,Roster Curry,\"", "unclosed quote"),
            (tooMany, "the league has 10"),
            ("Team,Player,Mine\nA,Roster Curry,yes\nB,Roster Doncic,yes", "Mark only one team"),
        })
        {
            using var rejected = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/teams/csv", new { Csv = csv }, token);
            rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await rejected.Content.ReadAsStringAsync(token)).ShouldContain(reason);
        }
    }

    [Fact]
    public async Task Trade_evaluation_scores_the_users_side_and_refuses_illegal_trades()
    {
        var token = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(token);
        var mine = await AddPlayerAsync("Trade Mine", token, "PG");
        var theirs = await AddPlayerAsync("Trade Theirs", token, "PG");
        await SeedProjectionAsync(mine, leagueId, token, seasonTotal: 700m);
        await SeedProjectionAsync(theirs, leagueId, token, seasonTotal: 900m);
        using (var imported = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/teams/csv",
            new { Csv = "Team,Player,Mine\nMine,Trade Mine,yes\nTheirs,Trade Theirs,\n" }, token))
        {
            imported.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        Guid myTeam, theirTeam;
        using (var listed = await client.GetAsync($"/api/leagues/{leagueId}/teams", token))
        using (var body = await ReadEnvelopeAsync(listed, token))
        {
            var teams = body.RootElement.GetProperty("data");
            myTeam = teams[0].GetProperty("id").GetGuid();
            theirTeam = teams[1].GetProperty("id").GetGuid();
        }

        using (var evaluated = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/trades/evaluate", new
        {
            Legs = new[]
            {
                new { FromTeamId = myTeam, ToTeamId = theirTeam, PlayerId = mine.Value },
                new { FromTeamId = theirTeam, ToTeamId = myTeam, PlayerId = theirs.Value },
            },
        }, token))
        {
            evaluated.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var body = await ReadEnvelopeAsync(evaluated, token);
            var data = body.RootElement.GetProperty("data");
            data.GetProperty("valueBefore").GetDecimal().ShouldBe(700m);
            data.GetProperty("valueAfter").GetDecimal().ShouldBe(900m);
            data.GetProperty("verdict").GetString().ShouldBe("ClearWin");
            data.GetProperty("sides")[0].GetProperty("team").GetString().ShouldBe("Mine");
        }

        using var illegal = await client.PostAsJsonAsync($"/api/leagues/{leagueId}/trades/evaluate", new
        {
            Legs = new[] { new { FromTeamId = theirTeam, ToTeamId = myTeam, PlayerId = mine.Value } },
        }, token);
        illegal.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await illegal.Content.ReadAsStringAsync(token)).ShouldContain("must come from the roster that sends him");
    }

    private async Task<Guid> CreateLeagueAsync(CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/leagues",
            LeagueRequest(
                [new { Stat = nameof(StatKey.PTS), PointsPerUnit = 1m }]),
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var document = await ReadEnvelopeAsync(response, cancellationToken);
        return document.RootElement.GetProperty("data")
            .GetProperty("id")
            .GetGuid();
    }

    private static object LeagueRequest(IReadOnlyList<object> scoringRules) =>
        new
        {
            Name = $"API League {Guid.NewGuid():N}",
            Type = "Points",
            TeamCount = 10,
            ScoringRules = scoringRules,
            Categories = Array.Empty<string>(),
            RosterSlots = new[] { "UTIL" },
            Cadence = "Daily",
        };

    private async Task<PlayerId> AddPlayerAsync(
        string name,
        CancellationToken cancellationToken,
        string position = "G")
    {
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            name,
            PlayerName.Normalize(name),
            null,
            [position],
            null);
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPlayerRepository>()
            .AddAsync(player, cancellationToken);
        return player.Id;
    }

    private async Task SeedProjectionAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken,
        bool hasUnverifiedContext = false,
        decimal seasonTotal = 700m)
    {
        var provenance = new DataProvenance(
            DataSourceName.Manual,
            null,
            DateTimeOffset.UnixEpoch,
            null,
            "manual-v1",
            DataSourceConfidence.ManualEntry,
            new string('a', 64));
        var source = new SeasonStatLine(
            playerId,
            2026,
            50,
            20m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 10m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 1000m,
                [StatKey.PTS] = 500m,
            }),
            null,
            provenance);
        var observed = new ObservedStats(
            playerId,
            source,
            DateTimeOffset.UnixEpoch);
        var baseline = new BaselineProjection(
            Guid.NewGuid(),
            playerId,
            20m,
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.PTS] = 0.5m,
            }),
            new StatLine(new Dictionary<StatKey, decimal>
            {
                [StatKey.MIN] = 20m,
                [StatKey.PTS] = 10m,
            }),
            70,
            DateTimeOffset.UnixEpoch,
            "api-test-v1");
        var adjusted = new AdjustedProjection(
            Guid.NewGuid(),
            playerId,
            baseline.Id,
            hasUnverifiedContext
                ? new StatLine(new Dictionary<StatKey, decimal>
                {
                    [StatKey.PTS] = 0.6m,
                })
                : baseline.ProjectedPerGame,
            [],
            hasUnverifiedContext ? 7m : 0m,
            Confidence.Moderate,
            1m,
            hasUnverifiedContext,
            DateTimeOffset.UnixEpoch);

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<ISeasonStatLineRepository>()
            .AddAsync(source, cancellationToken);
        var projections = services.GetRequiredService<IProjectionRepository>();
        await projections.AddAsync(observed, baseline, cancellationToken);
        await projections.AddAdjustedAsync(adjusted, cancellationToken);
        await projections.AddFantasyValueAsync(
            new FantasyValue(
                playerId,
                leagueId,
                10m,
                seasonTotal,
                adjusted.Id),
            (await services.GetRequiredService<ILeagueRepository>().GetAsync(leagueId, cancellationToken))!,
            DateTimeOffset.UnixEpoch,
            null,
            cancellationToken);
    }

    private async Task<Guid> CreateDraftAsync(
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/drafts",
            new
            {
                LeagueId = leagueId,
                DraftPosition = 1,
                RoundCount = 13,
            },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var document = await ReadEnvelopeAsync(response, cancellationToken);
        return document.RootElement.GetProperty("data")
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> RecordPickAsync(
        Guid draftId,
        int pickNumber,
        PlayerId playerId,
        HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/drafts/{draftId}/picks",
            new { PickNumber = pickNumber, PlayerId = playerId.Value },
            cancellationToken);
        response.StatusCode.ShouldBe(expected);
        using var document = await ReadEnvelopeAsync(response, cancellationToken);
        if (expected != HttpStatusCode.OK)
        {
            document.RootElement.GetProperty("error")
                .GetProperty("code")
                .GetString()
                .ShouldBe("conflict");
            return Guid.Empty;
        }

        return document.RootElement.GetProperty("data")
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> CreateContextEventAsync(
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/context-events",
            new
            {
                Type = "RotationChange",
                TeamId = (Guid?)null,
                PrimaryPlayerId = playerId.Value,
                AffectedPlayerIds = new[] { playerId.Value },
                EffectiveFrom = DateTimeOffset.UnixEpoch,
                ExpectedExpiration = (DateTimeOffset?)null,
                Direction = "Positive",
                Magnitude = 0.5m,
                Confidence = "High",
                SourceUrl = (string?)null,
                SourceName = "manual",
                RawText = (string?)null,
                Summary = "API context event",
            },
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var document = await ReadEnvelopeAsync(response, cancellationToken);
        return document.RootElement.GetProperty("data")
            .GetProperty("id")
            .GetGuid();
    }

    private static async Task AssertSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(expected);
            using var document = await ReadEnvelopeAsync(
                response,
                cancellationToken);
            document.RootElement.GetProperty("success")
                .GetBoolean()
                .ShouldBeTrue();
            document.RootElement.GetProperty("error")
                .ValueKind
                .ShouldBe(JsonValueKind.Null);
        }
    }

    private static async Task AssertFailureAsync(
        HttpResponseMessage response,
        HttpStatusCode expected,
        string code,
        CancellationToken cancellationToken)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(expected);
            using var document = await ReadEnvelopeAsync(
                response,
                cancellationToken);
            document.RootElement.GetProperty("success")
                .GetBoolean()
                .ShouldBeFalse();
            document.RootElement.GetProperty("data")
                .ValueKind
                .ShouldBe(JsonValueKind.Null);
            document.RootElement.GetProperty("error")
                .GetProperty("code")
                .GetString()
                .ShouldBe(code);
        }
    }

    private static async Task<JsonDocument> ReadEnvelopeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);
        document.RootElement.TryGetProperty("success", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("data", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("error", out _).ShouldBeTrue();
        document.RootElement.TryGetProperty("meta", out _).ShouldBeTrue();
        return document;
    }

    private static IResult ThrowEndpoint() =>
        throw new InvalidOperationException(
            "SQL fixture-connection-string stack trace");

    private sealed class FakeImportJobQueue(TimeProvider timeProvider)
        : IImportJobQueue
    {
        private readonly List<DataImportRun> active = [];

        public Task<DataImportRun> EnqueueAsync(
            ImportJobRequest request,
            CancellationToken cancellationToken)
        {
            var run = new DataImportRun(
                Guid.NewGuid(),
                DataSourceName.BallDontLie,
                DataImportRunStatus.Running,
                timeProvider.GetUtcNow(),
                null,
                0,
                0,
                null);
            active.Add(run);
            return Task.FromResult(run);
        }

        public IReadOnlyList<DataImportRun> GetActive() => active;
    }

    private sealed class FixedUserContext(Guid userId) : IUserContext
    {
        public Guid CurrentUserId => userId;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(
            options,
            logger,
            encoder)
    {
        public const string SchemeName = "ApiHttpTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, TestOwnerId()),
                new Claim(ClaimTypes.Name, "API Test"),
                new Claim(ClaimTypes.Role, "Owner"),
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }

        private string TestOwnerId() =>
            Context.RequestServices.GetRequiredService<IUserContext>()
                .CurrentUserId
                .ToString();
    }
}
