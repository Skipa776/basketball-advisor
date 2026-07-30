using System.Collections.Concurrent;
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
