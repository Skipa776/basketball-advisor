using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using FantasyBasketball.Api;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Domain.Context;
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
using Microsoft.Extensions.Options;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Api;

public sealed class ApiHttpTests : IAsyncLifetime
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
        await AssertSuccessAsync(
            await client.GetAsync(
                "/api/context-events?page=1&limit=50",
                cancellationToken),
            cancellationToken);

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
    public async Task A36_league_survives_the_visit_that_created_it()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(cancellationToken);

        // A later GET is a different circuit from the one that created the
        // league. My League held the league only in component state, so this
        // request rendered "No league configured" while the row sat in the
        // database -- and the draft page asked the user to paste "the ID
        // shown on My League", which by then was shown nowhere.
        using (var league = await client.GetAsync("/league", cancellationToken))
        {
            league.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await league.Content.ReadAsStringAsync(cancellationToken);
            html.ShouldContain(leagueId.ToString());
            html.Contains("No league configured", StringComparison.Ordinal)
                .ShouldBeFalse("a league exists, so the empty state is a lie");
        }

        using (var draft = await client.GetAsync("/draft", cancellationToken))
        {
            draft.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await draft.Content.ReadAsStringAsync(cancellationToken);
            var document = new HtmlParser().ParseDocument(html);
            document.QuerySelectorAll("#draft-league option")
                .Select(option => option.GetAttribute("value"))
                .ShouldContain(leagueId.ToString());
        }
    }

    [Fact]
    public async Task A20_A21_players_page_decomposes_and_marks_unverified_projection()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(cancellationToken);
        var playerId = await AddPlayerAsync(
            "Unverified UI Player",
            cancellationToken);
        await SeedProjectionAsync(
            playerId,
            leagueId,
            cancellationToken,
            hasUnverifiedContext: true);

        using var response = await client.GetAsync(
            $"/players?playerId={playerId.Value}&leagueId={leagueId}",
            cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        html.ShouldContain("Baseline projection");
        html.ShouldContain("Context adjustment");
        html.ShouldContain("Final projection");
        html.ShouldContain("Unverified context");
    }

    [Theory]
    [InlineData("/", "Dashboard")]
    [InlineData("/league", "My League")]
    [InlineData("/players", "Players")]
    [InlineData("/draft", "Draft Assistant")]
    [InlineData("/context-review", "Context Review")]
    [InlineData("/data-sources", "Data Sources")]
    public async Task Mvp_blazor_pages_render(
        string path,
        string heading)
    {
        using var response = await client.GetAsync(
            path,
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);
        document.QuerySelector("h1")?.TextContent.Trim().ShouldBe(heading);
    }

    public static TheoryData<string, string> PageThemeCases()
    {
        string[] pages =
        [
            "/",
            "/league",
            "/players",
            "/draft",
            "/context-review",
            "/data-sources",
            "/account/login",
            "/account/logout",
            "/account/register",
        ];
        string[] themes = ["dark", "light"];
        var cases = new TheoryData<string, string>();
        foreach (var page in pages)
        {
            foreach (var theme in themes)
            {
                cases.Add(page, theme);
            }
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(PageThemeCases))]
    public async Task D17_D18_every_page_has_empty_state_and_zero_accessibility_violations(
        string path,
        string theme)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Accessibility-Theme", theme);
        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var document = new HtmlParser().ParseDocument(html);

        document.QuerySelector(".empty-state").ShouldNotBeNull(
            $"{path} must render its first-run empty state in {theme}");
        AccessibilityViolations(document, path).ShouldBeEmpty(
            $"{path} has accessibility violations in {theme}");
    }

    [Fact]
    public async Task D13_D14_live_draft_commits_in_three_keys_and_has_no_visible_reflow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var leagueId = await CreateLeagueAsync(cancellationToken);
        string[] names =
        [
            "Zed UI Player",
            "Queue UI Player",
            .. Enumerable.Range(1, 48)
                .Select(index => $"Alpha UI Player {index:00}"),
        ];
        foreach (var name in names)
        {
            var playerId = await AddPlayerAsync(name, cancellationToken);
            await SeedProjectionAsync(
                playerId,
                leagueId,
                cancellationToken);
        }

        var repositoryRoot = Directory.GetParent(TestPaths.TestsRoot)!.FullName;
        var startInfo = new ProcessStartInfo("node")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("scripts/ui-browser-gate.mjs");
        startInfo.Environment["UI_BASE_URL"] = client.BaseAddress!.ToString();
        startInfo.Environment["UI_LEAGUE_ID"] = leagueId.ToString();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start the browser UI gate.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(
            cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(
            cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await standardOutput;
        var errorOutput = await standardError;

        process.ExitCode.ShouldBe(
            0,
            $"browser gate failed:\n{output}\n{errorOutput}");
        output.ShouldContain(
            "D-13 browser interaction: 3 keystrokes, pick committed, focus restored");
        output.ShouldContain("D-14 live Blazor measurement:");
        output.ShouldContain("0.000 CSS px maximum offset delta");
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

    private static IReadOnlyList<string> AccessibilityViolations(
        IDocument document,
        string path)
    {
        var violations = new List<string>();
        if (!string.Equals(
            document.DocumentElement?.GetAttribute("lang"),
            "en",
            StringComparison.OrdinalIgnoreCase))
        {
            violations.Add("document language is missing");
        }

        if (document.QuerySelectorAll("main").Length != 1)
        {
            violations.Add("page must have exactly one main landmark");
        }

        if (document.QuerySelectorAll("h1").Length != 1)
        {
            violations.Add("page must have exactly one h1");
        }

        foreach (var duplicate in document.QuerySelectorAll("[id]")
            .GroupBy(element => element.Id, StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key)
                && group.Count() > 1))
        {
            violations.Add($"duplicate id '{duplicate.Key}'");
        }

        foreach (var element in document.QuerySelectorAll(
            "[aria-describedby], [aria-labelledby], [aria-controls]"))
        {
            foreach (var attribute in new[]
                {
                    "aria-describedby",
                    "aria-labelledby",
                    "aria-controls",
                })
            {
                foreach (var id in (element.GetAttribute(attribute) ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (document.GetElementById(id) is null)
                    {
                        violations.Add(
                            $"{attribute} references missing id '{id}'");
                    }
                }
            }
        }

        foreach (var image in document.QuerySelectorAll("img"))
        {
            if (!image.HasAttribute("alt"))
            {
                violations.Add("image missing alt");
            }
        }

        foreach (var control in document.QuerySelectorAll(
            "input:not([type=hidden]), select, textarea"))
        {
            if (!HasAccessibleLabel(document, control))
            {
                violations.Add(
                    $"{control.LocalName}#{control.Id} has no accessible label");
            }
        }

        foreach (var button in document.QuerySelectorAll("button"))
        {
            if (!HasAccessibleName(button))
            {
                violations.Add("button has no accessible name");
            }
        }

        foreach (var link in document.QuerySelectorAll("a[href]"))
        {
            if (!HasAccessibleName(link))
            {
                violations.Add($"link to '{link.GetAttribute("href")}' has no accessible name");
            }
        }

        foreach (var table in document.QuerySelectorAll("table"))
        {
            if (table.QuerySelector("caption") is null)
            {
                violations.Add("table has no caption");
            }

            if (table.QuerySelector("th") is null)
            {
                violations.Add("table has no header cells");
            }
        }

        foreach (var navigation in document.QuerySelectorAll("nav"))
        {
            if (!navigation.HasAttribute("aria-label")
                && !navigation.HasAttribute("aria-labelledby"))
            {
                violations.Add("navigation landmark has no label");
            }
        }

        var liveRegionCount = document.QuerySelectorAll("[aria-live]").Length;
        if (path == "/draft")
        {
            if (liveRegionCount != 2)
            {
                violations.Add("draft page must expose exactly two live regions");
            }
        }
        else if (liveRegionCount != 0)
        {
            violations.Add("non-draft page exposes an unexpected live region");
        }

        return violations;
    }

    private static bool HasAccessibleLabel(
        IDocument document,
        IElement control)
    {
        if (!string.IsNullOrWhiteSpace(control.GetAttribute("aria-label"))
            || !string.IsNullOrWhiteSpace(control.GetAttribute("aria-labelledby")))
        {
            return true;
        }

        if (control.Closest("label") is not null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(control.Id)
            && document.QuerySelectorAll("label[for]")
                .Any(label => string.Equals(
                    label.GetAttribute("for"),
                    control.Id,
                    StringComparison.Ordinal));
    }

    private static bool HasAccessibleName(IElement element) =>
        !string.IsNullOrWhiteSpace(element.TextContent)
        || !string.IsNullOrWhiteSpace(element.GetAttribute("aria-label"))
        || !string.IsNullOrWhiteSpace(element.GetAttribute("aria-labelledby"))
        || !string.IsNullOrWhiteSpace(element.GetAttribute("title"));

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
        CancellationToken cancellationToken)
    {
        var player = new Player(
            new PlayerId(Guid.NewGuid()),
            name,
            PlayerName.Normalize(name),
            null,
            ["G"],
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
        bool hasUnverifiedContext = false)
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
                700m,
                adjusted.Id),
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
