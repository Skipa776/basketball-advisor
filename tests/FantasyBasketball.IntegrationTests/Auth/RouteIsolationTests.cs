using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FantasyBasketball.Api;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Testcontainers.PostgreSql;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class RouteIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();
    private readonly Guid userA = Guid.NewGuid();
    private readonly Guid userB = Guid.NewGuid();
    private readonly Guid playerId = Guid.NewGuid();
    private WebApplication app = null!;
    private HttpClient client = null!;
    private Guid leagueId;
    private Guid draftId;
    private Guid contextEventId;

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
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
                options.DefaultAuthenticateScheme =
                    HeaderAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme =
                    HeaderAuthenticationHandler.SchemeName;
                options.DefaultScheme = HeaderAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
                HeaderAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.RemoveAll<IHostedService>();
        app = builder.Build();
        ApiHost.Configure(app);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<FantasyDbContext>();
            await database.Database.MigrateAsync(
                TestContext.Current.CancellationToken);
            database.Users.AddRange(
                User(userA, "a"),
                User(userB, "b"));
            database.Players.Add(PlayerRow.Create(
                playerId,
                "Shared Isolation Player",
                "shared isolation player",
                ["G"],
                null));
            await database.SaveChangesAsync(
                TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        client = new HttpClient
        {
            BaseAddress = Address(app),
        };
        leagueId = await CreateLeagueAsUserAAsync();
        draftId = await CreateDraftAsUserAAsync();
        contextEventId = await CreateContextEventAsUserAAsync();
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.StopAsync(TestContext.Current.CancellationToken);
        await app.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task U01_U12_HP04_every_reflected_owned_route_returns_404_without_identifiers()
    {
        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<OwnedRouteMetadata>() is not null)
            .ToArray();

        routes.Length.ShouldBe(30);
        var failures = await SweepAsync(
            client,
            routes,
            userB,
            new SweepResources(leagueId, draftId, contextEventId, playerId));

        failures.ShouldBeEmpty();

        using var ownDrafts = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/drafts?leagueId={leagueId}",
            userA);
        ownDrafts.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ownDrafts.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldContain(draftId.ToString());
        using var foreignDrafts = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/drafts?leagueId={leagueId}",
            userB);
        foreignDrafts.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var leagueList = await SendAsync(client, HttpMethod.Get, "/api/leagues", userB);
        leagueList.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await leagueList.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldNotContain(leagueId.ToString());

        using var contextList = await SendAsync(
            client,
            HttpMethod.Get,
            "/api/context-events?page=1&limit=50",
            userB);
        contextList.StatusCode.ShouldBe(HttpStatusCode.OK);
        var listBody = await contextList.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        listBody.ShouldNotContain(contextEventId.ToString());
        listBody.ShouldNotContain(userA.ToString());
    }

    [Fact]
    public async Task U02_deliberately_unfiltered_owned_fixture_makes_sweep_fail()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    HeaderAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme =
                    HeaderAuthenticationHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(
                HeaderAuthenticationHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorization();
        await using var fixture = builder.Build();
        fixture.UseAuthentication();
        fixture.UseAuthorization();
        var leaked = new DeliberatelyUnfilteredOwnedResource(
            leagueId,
            userA);
        fixture.MapGet(
                "/fixture/{id:guid}",
                (Guid id) => Results.Ok(
                    id == leaked.Id ? leaked : null))
            .RequireAuthorization()
            .WithMetadata(new OwnedRouteMetadata("league", "id"));
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        using var fixtureClient = new HttpClient
        {
            BaseAddress = Address(fixture),
        };
        var routes = ((IEndpointRouteBuilder)fixture).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.Metadata.GetMetadata<OwnedRouteMetadata>() is not null)
            .ToArray();

        var failures = await SweepAsync(
            fixtureClient,
            routes,
            userB,
            new SweepResources(leagueId, draftId, contextEventId, playerId));

        routes.Length.ShouldBe(1);
        failures.Count.ShouldBe(1);
        failures[0].ShouldContain("/fixture/");
        await fixture.StopAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CreateLeagueAsUserAAsync()
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/leagues",
            userA,
            new
            {
                Name = "User A League",
                Type = "Points",
                TeamCount = 10,
                ScoringRules = new[]
                {
                    new { Stat = "PTS", PointsPerUnit = 1m },
                },
                Categories = Array.Empty<string>(),
                RosterSlots = new[] { "UTIL" },
                Cadence = "Daily",
            });
        return await ReadIdAsync(response);
    }

    private async Task<Guid> CreateDraftAsUserAAsync()
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/drafts",
            userA,
            new
            {
                LeagueId = leagueId,
                DraftPosition = 1,
                RoundCount = 13,
            });
        return await ReadIdAsync(response);
    }

    private async Task<Guid> CreateContextEventAsUserAAsync()
    {
        using var response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/context-events",
            userA,
            new
            {
                Type = "RotationChange",
                TeamId = (Guid?)null,
                PrimaryPlayerId = playerId,
                AffectedPlayerIds = new[] { playerId },
                EffectiveFrom = DateTimeOffset.UnixEpoch,
                ExpectedExpiration = (DateTimeOffset?)null,
                Direction = "Positive",
                Magnitude = 0.5m,
                Confidence = "High",
                SourceUrl = (string?)null,
                SourceName = "manual",
                RawText = (string?)null,
                Summary = "User A context",
            });
        return await ReadIdAsync(response);
    }

    private static async Task<IReadOnlyList<string>> SweepAsync(
        HttpClient http,
        IReadOnlyList<RouteEndpoint> routes,
        Guid attackingUser,
        SweepResources resources)
    {
        var failures = new List<string>();
        foreach (var endpoint in routes)
        {
            var method = endpoint.Metadata
                .GetMetadata<HttpMethodMetadata>()
                ?.HttpMethods
                .Single()
                ?? throw new InvalidOperationException(
                    "Owned routes must declare one HTTP method.");
            var path = BuildPath(endpoint.RoutePattern.RawText!, resources);
            var body = BuildBody(path, method, resources);
            using var response = await SendAsync(
                http,
                new HttpMethod(method),
                path,
                attackingUser,
                body);
            var responseBody = await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken);
            var leaked = responseBody.Contains(
                    resources.LeagueId.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                || responseBody.Contains(
                    resources.DraftId.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                || responseBody.Contains(
                    resources.ContextEventId.ToString(),
                    StringComparison.OrdinalIgnoreCase);
            if (response.StatusCode != HttpStatusCode.NotFound || leaked)
            {
                failures.Add(
                    $"{method} {path}: {(int)response.StatusCode}; {responseBody}");
            }
        }

        return failures;
    }

    private static string BuildPath(
        string route,
        SweepResources resources)
    {
        var routeId = route.StartsWith(
            "/api/players",
            StringComparison.Ordinal)
            ? resources.PlayerId
            : route.StartsWith("/api/drafts", StringComparison.Ordinal)
                ? resources.DraftId
                : route.StartsWith(
                    "/api/context-events",
                    StringComparison.Ordinal)
                    ? resources.ContextEventId
                    : resources.LeagueId;
        var path = Regex.Replace(
            route,
            @"\{id(?::[^}]+)?\}",
            routeId.ToString());
        path = Regex.Replace(
            path,
            @"\{pickNumber(?::[^}]+)?\}",
            "1");
        if (path.Contains("/projection", StringComparison.Ordinal)
            || path.Contains("/board", StringComparison.Ordinal))
        {
            path += $"?leagueId={resources.LeagueId}";
        }

        if (route is "/api/drafts/" or "/api/drafts")
        {
            path += $"?leagueId={resources.LeagueId}";
        }

        if (path.EndsWith("/performance", StringComparison.Ordinal))
            path += $"?seasonEndYear=2026&source={FantasyBasketball.Domain.Provenance.DataSourceName.Manual}&throughDate=2026-01-14&view=best";
        if (path.EndsWith("/heat-labels", StringComparison.Ordinal))
            path += $"?seasonEndYear=2026&source={FantasyBasketball.Domain.Provenance.DataSourceName.Manual}&throughDate=2026-01-14";
        return path;
    }

    private static object? BuildBody(
        string path,
        string method,
        SweepResources resources)
    {
        if (path.EndsWith("/providers/sleeper/import", StringComparison.Ordinal))
        {
            return new { SleeperLeagueId = "1", MyTeamId = "1" };
        }

        if (path.EndsWith("/trades/evaluate", StringComparison.Ordinal))
        {
            return new { Legs = new[] { new { FromTeamId = Guid.NewGuid(), ToTeamId = Guid.NewGuid(), PlayerId = Guid.NewGuid() } } };
        }

        if (path.EndsWith("/teams/csv", StringComparison.Ordinal))
        {
            return new { Csv = "Team,Player\nForeign,Nobody" };
        }

        if (path.Contains("/projections", StringComparison.Ordinal) && method == HttpMethods.Post)
        {
            return new { SeasonEndYear = 2026, Source = FantasyBasketball.Domain.Provenance.DataSourceName.Manual };
        }

        if (method == HttpMethods.Put)
        {
            if (path.EndsWith("/settings", StringComparison.Ordinal))
            {
                return new
                {
                    Name = "Foreign league",
                    TeamCount = 10,
                    Cadence = "Daily",
                    RosterSlots = new[] { "UTIL" },
                };
            }

            return new
            {
                ScoringRules = new[]
                {
                    new { Stat = "PTS", PointsPerUnit = 2m },
                },
            };
        }

        if (path == "/api/drafts" || path == "/api/drafts/")
        {
            return new
            {
                LeagueId = resources.LeagueId,
                DraftPosition = 1,
                RoundCount = 13,
            };
        }

        if (path.Contains("/picks", StringComparison.Ordinal)
            && method == HttpMethods.Post)
        {
            return new { PickNumber = 1, PlayerId = resources.PlayerId };
        }

        if (path.Contains("/verify", StringComparison.Ordinal)
            || path.Contains("/reject", StringComparison.Ordinal))
        {
            return new { UserId = Guid.NewGuid() };
        }

        return null;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient http,
        HttpMethod method,
        string path,
        Guid userId,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(HeaderAuthenticationHandler.UserHeader, userId.ToString());
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await http.SendAsync(
            request,
            TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("data")
            .GetProperty("id")
            .GetGuid();
    }

    private static FantasyUser User(Guid id, string suffix) =>
        new()
        {
            Id = id,
            UserName = $"isolation-{suffix}",
            NormalizedUserName = $"ISOLATION-{suffix.ToUpperInvariant()}",
            DisplayName = $"Isolation {suffix}",
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    private static Uri Address(WebApplication application)
    {
        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException(
                "Kestrel did not publish an address.");
        return new Uri(addresses.Single());
    }

    private sealed record SweepResources(
        Guid LeagueId,
        Guid DraftId,
        Guid ContextEventId,
        Guid PlayerId);

    private sealed record DeliberatelyUnfilteredOwnedResource(
        Guid Id,
        Guid? OwnerId) : IOwnedResource;

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(
            options,
            logger,
            encoder)
    {
        public const string SchemeName = "IsolationHeader";
        public const string UserHeader = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var value)
                || !Guid.TryParse(value, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"Test user {userId}"),
                new Claim(ClaimTypes.Role, "Owner"),
            };
            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
