using System.Collections.Concurrent;
using System.Diagnostics;
using FantasyBasketball.IntegrationTests.TestSupport;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FantasyBasketball.Api;
using FantasyBasketball.Application.Ingestion;
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
    public async Task React_browser_registers_creates_league_and_persists_keyboard_pick()
    {
        var token = TestContext.Current.CancellationToken;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<FantasyDbContext>();
            database.Players.Add(PlayerRow.Create(Guid.NewGuid(), "Fixture Guard", "fixture guard", ["PG"], null));
            await database.SaveChangesAsync(token);
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
        html.ShouldContain("Draft with evidence");
        html.ShouldContain("Bring your league from");
        // Nominative use, and the disclaimer travels with it.
        html.ShouldContain("Not affiliated with");

        // The dashboard's own sections read owned tables. Their absence is what
        // makes this a landing page rather than a dashboard that happened to
        // survive; it is also the observable half of the guard, because the
        // page's catch would otherwise swallow the CurrentUserId throw and
        // render the same 200 either way.
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
    /// D-26. Row A-26 requires demo data to be labelled fictional and opt-in.
    /// This fixture leaves the flag off, so the landing page must carry neither
    /// the badge nor any of the invented players.
    /// </summary>
    [Fact]
    public async Task D26_sample_content_is_absent_until_the_demo_flag_is_set()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync("/", cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        html.Contains("data-demo-label", StringComparison.Ordinal)
            .ShouldBeFalse("the sample badge must not render with the flag off");
        foreach (var invented in new[] { "Dario Vance", "Emeka Baptiste", "Example Wire" })
        {
            html.Contains(invented, StringComparison.Ordinal)
                .ShouldBeFalse($"'{invented}' is demo content and the flag is off");
        }
    }

    /// <summary>
    /// D-26, the other half: with the flag on, every surface that renders
    /// invented content also renders the label that says so.
    /// </summary>
    [Fact]
    public async Task D26_sample_content_is_labelled_fictional_when_enabled()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
            options.Listen(IPAddress.Loopback, 0));
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "fixture-api-key",
                ["ConnectionStrings:Fantasy"] = postgres.GetConnectionString(),
                ["Demo:Enabled"] = "true",
            });
        ApiHost.ConfigureServices(builder);
        builder.Services.RemoveAll<IHostedService>();
        var demoApp = builder.Build();
        ApiHost.Configure(demoApp);
        await demoApp.StartAsync(cancellationToken);

        try
        {
            using var demoClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
            })
            {
                BaseAddress = Address(demoApp),
            };

            using var response = await demoClient.GetAsync("/", cancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            html.ShouldContain("Dario Vance");
            html.ShouldContain("data-demo-label");
            html.ShouldContain("Fictional players and headlines");
        }
        finally
        {
            await demoApp.StopAsync(cancellationToken);
            await demoApp.DisposeAsync();
        }
    }

    [Fact]
    public async Task U18_register_and_log_in_through_the_rendered_html_form()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // Anonymous static assets first: without them the login page renders with
        // no stylesheet and Blazor never boots, which is the same outage wearing a
        // different hat -- and invisible to any test that only parses the HTML.
        foreach (var asset in new[]
            {
                "/FantasyBasketball.Api.styles.css",
                "/_framework/blazor.web.js",
            })
        {
            using var assetResponse = await client.GetAsync(asset, cancellationToken);
            assetResponse.StatusCode.ShouldBe(
                HttpStatusCode.OK,
                $"{asset} must be reachable without authentication");
        }

        var (registerAction, registerToken, registerCookie) =
            await ReadFormAsync("/account/register", cancellationToken);
        using var registered = await PostFormAsync(
            registerAction,
            registerCookie,
            new Dictionary<string, string>
            {
                ["displayName"] = "Form User",
                ["email"] = "form-user@local.test",
                ["password"] = "form-password-1234",
                ["__RequestVerificationToken"] = registerToken,
            },
            cancellationToken);
        registered.StatusCode.ShouldBe(
            HttpStatusCode.Redirect,
            "the register form's own action must reach its handler");

        var (loginAction, loginToken, loginCookie) =
            await ReadFormAsync("/account/login", cancellationToken);
        using var loggedIn = await PostFormAsync(
            loginAction,
            loginCookie,
            new Dictionary<string, string>
            {
                ["email"] = "form-user@local.test",
                ["password"] = "form-password-1234",
                ["__RequestVerificationToken"] = loginToken,
            },
            cancellationToken);
        loggedIn.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        loggedIn.Headers.Location!.OriginalString.ShouldBe("/");

        var authCookie = loggedIn.Headers.GetValues("Set-Cookie")
            .Select(header => header.Split(';')[0])
            .First(header => header.StartsWith(".AspNetCore.Identity", StringComparison.Ordinal));
        using var dashboard = new HttpRequestMessage(HttpMethod.Get, "/");
        dashboard.Headers.Add("Cookie", authCookie);
        using var dashboardResponse = await client.SendAsync(dashboard, cancellationToken);
        dashboardResponse.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            "the cookie the form issued must authenticate a page request");
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
