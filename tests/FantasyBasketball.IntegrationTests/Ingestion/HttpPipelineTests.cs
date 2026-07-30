using System.Diagnostics;
using System.Net;
using FantasyBasketball.Api;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FantasyBasketball.Api.Options;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Ingestion;

public sealed class HttpPipelineTests
{
    [Fact]
    public async Task I12_second_get_inside_freshness_window_uses_cache()
    {
        var terminal = new RecordingHandler();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var pipeline = new HttpMessageInvoker(
            new ResponseCacheHandler(cache, TimeSpan.FromDays(1))
            {
                InnerHandler = terminal,
            });

        using var first = await pipeline.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://example.test/players"),
            TestContext.Current.CancellationToken);
        using var second = await pipeline.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "https://example.test/players"),
            TestContext.Current.CancellationToken);

        (await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe("fixture");
        (await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .ShouldBe("fixture");
        terminal.SendCount.ShouldBe(1);
    }

    [Fact]
    public async Task S11_twenty_requests_share_one_per_host_rate_limiter()
    {
        await using var limiter = new HostRateLimiter(TimeSpan.FromMilliseconds(5));
        using var firstPipeline = new HttpMessageInvoker(
            new HostRateLimitHandler(limiter)
            {
                InnerHandler = new RecordingHandler(),
            });
        using var secondPipeline = new HttpMessageInvoker(
            new HostRateLimitHandler(limiter)
            {
                InnerHandler = new RecordingHandler(),
            });
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < 20; index++)
        {
            var pipeline = index % 2 == 0 ? firstPipeline : secondPipeline;
            using var response = await pipeline.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://example.test/data"),
                TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        stopwatch.Stop();
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(70));
    }

    [Fact]
    public async Task Named_factory_clients_use_resilience_without_network_access()
    {
        var terminal = new RecordingHandler(HttpStatusCode.InternalServerError);
        var services = new ServiceCollection();
        services.AddExternalDataHttpClients(CreateConfiguration());
        services.AddSingleton(new HostRateLimiter(TimeSpan.FromMilliseconds(1)));
        services.AddHttpClient(DataSourceName.BallDontLie)
            .ConfigurePrimaryHttpMessageHandler(() => terminal);
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(DataSourceName.BallDontLie);

        using var response = await client.GetAsync(
            "/v1/players",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        terminal.SendCount.ShouldBeGreaterThan(1);
        client.BaseAddress.ShouldBe(new Uri("https://api.balldontlie.io"));
    }

    [Fact]
    public void A15_missing_api_key_names_the_configuration_key()
    {
        var services = new ServiceCollection();
        services.AddExternalDataHttpClients(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Should.Throw<OptionsValidationException>(() =>
            serviceProvider.GetRequiredService<IOptions<BallDontLieOptions>>().Value);

        exception.Message.ShouldContain("BallDontLie:ApiKey");
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "test",
            })
            .Build();

    private sealed class RecordingHandler(
        HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("fixture"),
                RequestMessage = request,
            });
        }
    }
}
