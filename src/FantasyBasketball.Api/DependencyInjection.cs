using FantasyBasketball.Api.Options;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Application.Context;
using FantasyBasketball.Application.Draft;
using FantasyBasketball.Application.Health;
using FantasyBasketball.Application.Leagues;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Infrastructure.Http;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using FantasyBasketball.Infrastructure.Providers.BallDontLie;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using FantasyBasketball.Infrastructure.Scrapers.FantasyPros;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Api;

public static class DependencyInjection
{
    private static readonly TimeSpan CacheFreshness = TimeSpan.FromDays(1);
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(10);
    private const string UserAgent =
        "FantasyBasketballDecisionEngine/0.1 (personal project; contact via repository)";

    public static IServiceCollection AddExternalDataHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BallDontLieOptions>()
            .Bind(configuration.GetSection(BallDontLieOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApiKey),
                "BallDontLie:ApiKey is required.")
            .ValidateOnStart();
        services.AddOptions<ProjectionOptions>()
            .Bind(configuration.GetSection(ProjectionOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Projection options are invalid.")
            .ValidateOnStart();
        services.AddOptions<DraftWeightOptions>()
            .Bind(configuration.GetSection(DraftWeightOptions.SectionName))
            .Validate(options => options.IsValid(), "Draft weights are invalid.")
            .ValidateOnStart();
        services.AddOptions<DataSourceHealthOptions>()
            .Bind(configuration.GetSection(DataSourceHealthOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Data source health options are invalid.")
            .ValidateOnStart();
        services.AddDbContext<FantasyDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Fantasy")));
        services.AddMemoryCache();
        services.AddSingleton(new HostRateLimiter(MinimumRequestInterval));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<ProjectionOptions>>().Value);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<DraftWeightOptions>>().Value);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<DataSourceHealthOptions>>().Value);
        services.AddSingleton<MinutesProjector>();
        services.AddSingleton<BaselineProjector>();
        services.AddSingleton<DraftValueCalculator>();
        services.AddSingleton<DraftBoard>();
        services.AddSingleton<ConfidenceCalculator>();
        services.AddSingleton<ContextApplier>();
        services.AddSingleton<DraftRecommendationEngine>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IPlayerQueryRepository, PlayerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IAdpRepository, AdpRepository>();
        services.AddScoped<ILeagueRepository, LeagueRepository>();
        services.AddScoped<ISeasonStatLineRepository, SeasonStatLineRepository>();
        services.AddScoped<IProjectionRepository, ProjectionRepository>();
        services.AddScoped<IProjectionQueryRepository, ProjectionRepository>();
        services.AddScoped<IContextEventRepository, ContextEventRepository>();
        services.AddScoped<IContextEventQueryRepository, ContextEventRepository>();
        services.AddScoped<IDraftRepository, DraftRepository>();
        services.AddScoped<IDraftCandidateRepository, DraftCandidateRepository>();
        services.AddScoped<IDataImportRunRepository, DataImportRunRepository>();
        services.AddScoped<IImportRunQueryRepository, DataImportRunRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IImportTransaction, EfImportTransaction>();
        services.AddScoped<PlayerIdentityResolver>();
        services.AddScoped<ImportPlayersService>();
        services.AddScoped<ImportDirectoryService>();
        services.AddScoped<ImportScheduleService>();
        services.AddScoped<ImportSeasonStatsService>();
        services.AddScoped<ImportAdpService>();
        services.AddScoped<ImportRunQueryService>();
        services.AddScoped<ProjectionService>();
        services.AddScoped<ProjectionDecompositionService>();
        services.AddScoped<ContextEventService>();
        services.AddScoped<ContextEventQueryService>();
        services.AddScoped<DraftSessionService>();
        services.AddScoped<DraftBoardService>();
        services.AddScoped<LeagueService>();
        services.AddScoped<PlayerQueryService>();
        services.AddScoped<DataSourceHealthService>();
        services.AddScoped<BallDontLieProvider>();
        services.AddScoped<IPlayerDirectoryProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<BallDontLieProvider>());
        services.AddScoped<IScheduleProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<BallDontLieProvider>());
        services.AddScoped<BasketballReferenceStatsScraper>();
        services.AddScoped<IPlayerStatsProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<BasketballReferenceStatsScraper>());
        services.AddScoped<FantasyProsAdpScraper>();
        services.AddScoped<IAdpProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<FantasyProsAdpScraper>());
        services.AddSingleton<ImportJobQueue>();
        services.AddSingleton<IImportJobQueue>(serviceProvider =>
            serviceProvider.GetRequiredService<ImportJobQueue>());
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<ImportJobQueue>());

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
                (serviceProvider, client) =>
                {
                    client.BaseAddress = baseAddress;
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                    if (name == DataSourceName.BallDontLie)
                    {
                        var apiKey = serviceProvider
                            .GetRequiredService<IOptions<BallDontLieOptions>>()
                            .Value
                            .ApiKey;
                        client.DefaultRequestHeaders.TryAddWithoutValidation(
                            "Authorization",
                            apiKey);
                    }
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
