using System.Collections.Concurrent;
using System.Diagnostics;
using FantasyBasketball.IntegrationTests.TestSupport;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FantasyBasketball.Api;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shouldly;
using Testcontainers.PostgreSql;

// s12-allow: loopback self-host -- drives the in-process Kestrel app under test.
namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class AuthHttpTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres =
        new PostgreSqlBuilder("postgres:17").Build();
    private readonly RecordingLoggerProvider logs = new();
    private WebApplication app = null!;
    private HttpClient client = null!;
    private string antiforgeryToken = string.Empty;
    private string antiforgeryCookie = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ApiHost).Assembly.GetName().Name,
            WebRootPath = Path.Combine(Directory.GetParent(TestPaths.TestsRoot)!.FullName, "src/FantasyBasketball.Api/wwwroot"),
            EnvironmentName = Environments.Development,
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "fixture-api-key",
                ["ConnectionStrings:Fantasy"] = postgres.GetConnectionString(),
            });
        ApiHost.ConfigureServices(builder);
        builder.Services.RemoveAll<IHostedService>();
        app = builder.Build();
        ApiHost.Configure(app);

        await using (var scope = app.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<FantasyDbContext>()
                .Database
                .MigrateAsync(TestContext.Current.CancellationToken);
        }

        await app.StartAsync(TestContext.Current.CancellationToken);
        client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
        })
        {
            BaseAddress = Address(app),
        };
        await GetAntiforgeryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        client.Dispose();
        await app.StopAsync(TestContext.Current.CancellationToken);
        await app.DisposeAsync();
        await postgres.DisposeAsync();
        logs.Dispose();
    }

    [Fact]
    public async Task HP04_anonymous_performance_queries_require_authentication()
    {
        foreach (var path in new[] { "performance-pools", "performance?seasonEndYear=2026&source=manual&throughDate=2026-01-14&view=best" })
        {
            using var response = await client.GetAsync($"/api/leagues/{Guid.NewGuid()}/{path}", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Anonymous_react_bundle_added_after_host_start_is_served_as_javascript()
    {
        var assetDirectory = Path.Combine(app.Environment.WebRootPath, "app", "assets");
        Directory.CreateDirectory(assetDirectory);
        var assetName = $"test-{Guid.NewGuid():N}.js";
        var assetPath = Path.Combine(assetDirectory, assetName);
        try
        {
            await File.WriteAllTextAsync(
                assetPath, "export const ready = true;", TestContext.Current.CancellationToken);
            using var response = await client.GetAsync(
                $"/app/assets/{assetName}", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.ShouldBe("text/javascript");
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
                .ShouldContain("ready = true");
        }
        finally
        {
            File.Delete(assetPath);
        }
    }

    [Fact]
    public async Task HP05_U18_A20_A36_D13_D14_D18_D30_React_browser_registers_creates_league_and_persists_keyboard_pick()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FantasyDbContext>();
            foreach (var (name, points) in new[] { ("Fixture Guard", 30m), ("Fixture Center", 20m) })
            {
                var playerId = new PlayerId(Guid.NewGuid());
                database.Players.Add(PlayerRow.Create(playerId.Value, name, name.ToLowerInvariant(), ["PG"], null));
                await database.SaveChangesAsync(token);
                await scope.ServiceProvider.GetRequiredService<ISeasonStatLineRepository>().AddAsync(
                    new SeasonStatLine(playerId, 2026, 50, 30m,
                        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.PTS] = points }),
                        new StatLine(new Dictionary<StatKey, decimal> { [StatKey.MIN] = 1500m, [StatKey.PTS] = points * 50m }),
                        null, new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null,
                            "manual-v1", DataSourceConfidence.ManualEntry, new string('a', 64))), token);
            }
        }

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FantasyDbContext>();
            var guard = await database.Players.SingleAsync(player => player.FullName == "Fixture Guard", token);
            var center = await database.Players.SingleAsync(player => player.FullName == "Fixture Center", token);
            await RecordedGameFixture.SeedAsync(database, guard.Id, center.Id, null, token);
        }

        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = Directory.GetParent(TestPaths.TestsRoot)!.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("src/FantasyBasketball.Web/tests/workspace.mjs");
        start.Environment["UI_BASE_URL"] = client.BaseAddress!.ToString();
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(token);
        var errors = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        process.ExitCode.ShouldBe(0, $"{await output}\n{await errors}");
    }

    [Fact]
    public async Task Session_discovery_tracks_registration_and_identity_without_caching()
    {
        var token = TestContext.Current.CancellationToken;
        using var anonymous = await client.GetAsync("/api/account/session", token);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.OK);
        anonymous.Headers.CacheControl!.NoStore.ShouldBeTrue();
        using var initial = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync(token));
        initial.RootElement.GetProperty("data").GetProperty("authenticated").GetBoolean().ShouldBeFalse();
        initial.RootElement.GetProperty("data").GetProperty("registrationOpen").GetBoolean().ShouldBeTrue();

        using var registration = await PostAsync("/api/account/register", new
        {
            Email = "session@example.test",
            Password = string.Join(' ', "fixture", "session", "password"),
            DisplayName = "Session owner",
        }, token);
        registration.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cookie = registration.Headers.GetValues("Set-Cookie")
            .Single(value => value.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal))
            .Split(';')[0];
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/account/session");
        request.Headers.Add("Cookie", cookie);
        using var authenticated = await client.SendAsync(request, token);
        authenticated.Headers.CacheControl!.NoStore.ShouldBeTrue();
        using var session = JsonDocument.Parse(await authenticated.Content.ReadAsStringAsync(token));
        var data = session.RootElement.GetProperty("data");
        data.GetProperty("authenticated").GetBoolean().ShouldBeTrue();
        data.GetProperty("registrationOpen").GetBoolean().ShouldBeFalse();
        data.GetProperty("user").GetProperty("displayName").GetString().ShouldBe("Session owner");
        data.GetProperty("user").EnumerateObject().Count().ShouldBe(3);
        using var anonymousAgain = await client.GetAsync("/api/account/session", token);
        using var closed = JsonDocument.Parse(await anonymousAgain.Content.ReadAsStringAsync(token));
        closed.RootElement.GetProperty("data").GetProperty("authenticated").GetBoolean().ShouldBeFalse();
        closed.RootElement.GetProperty("data").GetProperty("registrationOpen").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task U06_U07_U08_U09_U10_cookie_registration_and_lockout_defaults_hold()
    {
        const string email = "owner@example.test";
        const string password = "correct horse battery staple";
        var cancellationToken = TestContext.Current.CancellationToken;

        using var registration = await PostAsync(
            "/api/account/register",
            new
            {
                Email = email,
                Password = password,
                DisplayName = "Instance Owner",
            },
            cancellationToken);
        registration.StatusCode.ShouldBe(HttpStatusCode.Created);
        var identityCookie = registration.Headers.GetValues("Set-Cookie")
            .Single(value => value.Contains(".AspNetCore.Identity.Application"));
        identityCookie.ShouldContain("httponly", Case.Insensitive);
        identityCookie.ShouldContain("secure", Case.Insensitive);
        identityCookie.ShouldContain("samesite=lax", Case.Insensitive);

        using var closed = await PostAsync(
            "/api/account/register",
            new
            {
                Email = "second@example.test",
                Password = "another correct horse battery",
                DisplayName = "Second",
            },
            cancellationToken);
        closed.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var failed = await PostAsync(
                "/api/account/login",
                new
                {
                    Email = email,
                    Password = "definitely wrong password",
                    RememberMe = false,
                },
                cancellationToken);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            if (attempt == 5)
            {
                var body = await failed.Content.ReadAsStringAsync(
                    cancellationToken);
                body.ShouldContain("locked_out");
            }
        }

        using var locked = await PostAsync(
            "/api/account/login",
            new
            {
                Email = email,
                Password = password,
                RememberMe = false,
            },
            cancellationToken);
        (await locked.Content.ReadAsStringAsync(cancellationToken))
            .ShouldContain("locked_out");

        await using var scope = app.Services.CreateAsyncScope();
        var hash = (await scope.ServiceProvider
                .GetRequiredService<FantasyDbContext>()
                .Users
                .SingleAsync(cancellationToken))
            .PasswordHash;
        hash.ShouldNotBeNullOrWhiteSpace();
        var cookieValue = identityCookie.Split(';')[0].Split('=', 2)[1];
        logs.Messages.ShouldAllBe(message =>
            !message.Contains(password, StringComparison.Ordinal)
            && !message.Contains(hash, StringComparison.Ordinal)
            && !message.Contains(cookieValue, StringComparison.Ordinal));
    }

    [Fact]
    public async Task U08_production_http_is_redirected_to_https()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
        });
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "fixture-api-key",
                ["ConnectionStrings:Fantasy"] =
                    "Host=localhost;Database=unused;Username=unused;Password=unused",
            });
        ApiHost.ConfigureServices(builder);
        builder.Services.RemoveAll<IHostedService>();
        await using var production = builder.Build();
        ApiHost.Configure(production);
        await production.StartAsync(TestContext.Current.CancellationToken);
        using var http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
        })
        {
            BaseAddress = Address(production),
        };

        using var response = await http.GetAsync(
            "/",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location?.Scheme.ShouldBe(Uri.UriSchemeHttps);
        await production.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Row U-18 — the path a human actually takes. Every other row here posts JSON
    /// to /api/account/*, which left the rendered form untested: a Razor page route
    /// answers every HTTP method, so a MapPost on the page's own path made routing
    /// throw AmbiguousMatchException before either handler ran. Logging in through
    /// the UI was impossible while the whole API suite stayed green.
    /// </summary>
    /// <summary>
    /// D-25. The landing page is the one page an anonymous visitor reaches, and
    /// the dashboard behind the same route reads owned tables. HttpUserContext
    /// throws rather than returning empty without a request user, so a query
    /// left un-branched turns the front door into a 500.
    /// </summary>
    [Fact]
    public async Task D25_landing_page_renders_anonymously_without_touching_owned_data()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync("/", cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        // `/` serves the React shell; the app decides landing vs workspace.
        html.ShouldContain("<div id=\"root\">");

        foreach (var authenticatedOnly in new[]
            {
                "Data freshness",
                "Recent imports",
                "Data status could not be loaded",
            })
        {
            html.Contains(authenticatedOnly, StringComparison.Ordinal)
                .ShouldBeFalse(
                    $"'{authenticatedOnly}' reads owned data and must not reach "
                    + "an anonymous visitor");
        }

        logs.Messages.ShouldAllBe(message =>
            !message.Contains(
                "Owned data requires an authenticated request user",
                StringComparison.Ordinal));
    }


    /// <summary>
    /// Reads the form's declared action and anti-forgery token straight out of the
    /// rendered page, so the test follows whatever path the markup posts to rather
    /// than a path hard-coded here that could drift away from it.
    /// </summary>
    private async Task<(string Action, string Token, string Cookie)> ReadFormAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';')[0])
            .First(header => header.StartsWith(".AspNetCore.Antiforgery", StringComparison.Ordinal));
        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(
            await response.Content.ReadAsStringAsync(cancellationToken));
        var form = document.QuerySelector("form[method=post]")
            ?? throw new InvalidOperationException($"{path} rendered no post form.");
        var action = form.GetAttribute("action")
            ?? throw new InvalidOperationException($"{path} form declares no action.");
        var token = form.QuerySelector("input[name=__RequestVerificationToken]")
            ?.GetAttribute("value")
            ?? throw new InvalidOperationException($"{path} form has no token.");
        return (action, token, cookie);
    }

    private async Task<HttpResponseMessage> PostFormAsync(
        string path,
        string antiforgeryCookieHeader,
        Dictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields),
        };
        request.Headers.Add("Cookie", antiforgeryCookieHeader);
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task GetAntiforgeryAsync()
    {
        using var response = await client.GetAsync(
            "/api/account/antiforgery",
            TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        antiforgeryCookie = response.Headers.GetValues("Set-Cookie")
            .Single()
            .Split(';')[0];
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
        antiforgeryToken = document.RootElement.GetProperty("data")
            .GetProperty("token")
            .GetString()
            ?? throw new InvalidOperationException("No anti-forgery token.");
    }

    private async Task<HttpResponseMessage> PostAsync(
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("Cookie", antiforgeryCookie);
        request.Headers.Add("X-CSRF-TOKEN", antiforgeryToken);
        return await client.SendAsync(request, cancellationToken);
    }

    private static Uri Address(WebApplication application)
    {
        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()
            ?.Addresses
            ?? throw new InvalidOperationException(
                "Kestrel did not publish an address.");
        return new Uri(addresses.Single());
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyList<string> Messages => messages.ToArray();

        public ILogger CreateLogger(string categoryName) =>
            new RecordingLogger(messages);

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(
        ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }
}
