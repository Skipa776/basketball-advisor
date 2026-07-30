using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Http;

namespace FantasyBasketball.Api;

public static class DependencyInjection
{
    private static readonly TimeSpan CacheFreshness = TimeSpan.FromDays(1);
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(10);
    private const string UserAgent =
        "FantasyBasketballDecisionEngine/0.1 (personal project; contact via repository)";

    public static IServiceCollection AddExternalDataHttpClients(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(new HostRateLimiter(MinimumRequestInterval));

        AddSourceClient(
            services,
            DataSourceName.BallDontLie,
            new Uri("https://api.balldontlie.io"));
        AddSourceClient(
            services,
            DataSourceName.BasketballReference,
            new Uri("https://www.basketball-reference.com"));
        AddSourceClient(
            services,
            DataSourceName.FantasyPros,
            new Uri("https://www.fantasypros.com"));

        return services;
    }

    private static void AddSourceClient(
        IServiceCollection services,
        string name,
        Uri baseAddress)
    {
        var clientBuilder = services.AddHttpClient(
                name,
                client =>
                {
                    client.BaseAddress = baseAddress;
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                })
            .AddHttpMessageHandler(serviceProvider =>
                new ResponseCacheHandler(
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                    CacheFreshness));
        clientBuilder.AddStandardResilienceHandler();
        clientBuilder.AddHttpMessageHandler(serviceProvider =>
            new HostRateLimitHandler(
                serviceProvider.GetRequiredService<HostRateLimiter>()));
    }
}
