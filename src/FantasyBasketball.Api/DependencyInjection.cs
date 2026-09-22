using FantasyBasketball.Api.Options;
using FantasyBasketball.Api.Middleware;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Accounts;
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
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Persistence;
using FantasyBasketball.Infrastructure.Persistence.Repositories;
using FantasyBasketball.Infrastructure.Providers.BallDontLie;
using FantasyBasketball.Infrastructure.Scrapers.BasketballReference;
using FantasyBasketball.Infrastructure.Scrapers.FantasyPros;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Api;

public static class DependencyInjection
{
    private static readonly TimeSpan CacheFreshness = TimeSpan.FromDays(1);
    /// <summary>
    /// Floor between requests to any one external host: 10s, or six a minute.
    /// That is the self-imposed scrape ceiling in safety/scraping_policy.md and
    /// it stays the default.
    ///
    /// Configurable because a published API limit is not the same number as a
    /// politeness ceiling, and balldontlie's free tier is five a minute -- one
    /// tighter than this. A player import paginates far enough that the
    /// difference is the whole run: teams (one request) succeeded while players
    /// earned a 429 partway through. Raising the interval is the operator's
    /// lever; nothing here may ever LOWER it below the policy floor.
    /// </summary>
    private const int DefaultRequestIntervalSeconds = 10;
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
        services.AddOptions<RefreshWorkerOptions>()
            .Bind(configuration.GetSection(RefreshWorkerOptions.SectionName))
            .Validate(
                options => options.IsValid(),
                "Refresh worker options are invalid.")
            .ValidateOnStart();
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName));
        services.AddOptions<Components.Design.DemoContentOptions>()
            .Bind(configuration.GetSection(
                Components.Design.DemoContentOptions.SectionName));
        services.AddOptions<GoogleSignInOptions>()
            .Bind(configuration.GetSection(GoogleSignInOptions.SectionName));
        services.AddDbContext<FantasyDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Fantasy")));
        services.AddIdentity<FantasyUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<FantasyDbContext>()
            .AddDefaultTokenProviders();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
        services.AddCascadingAuthenticationState();
        services.AddHttpContextAccessor();
        services.AddScoped<ActiveLeague>();
        services.AddHttpsRedirection(options =>
        {
            options.HttpsPort = 443;
            options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        services.AddScoped<CookieAntiforgeryFilter>();
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("account", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueLimit = 0;
                limiter.AutoReplenishment = true;
            });
        });
        services.AddScoped<IUserContext, HttpUserContext>();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = "/account/login";
            options.Events.OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
                else
                {
                    context.Response.Redirect(context.RedirectUri);
                }

                return Task.CompletedTask;
            };
        });
        services.AddMemoryCache();
        var requestIntervalSeconds = Math.Max(
            DefaultRequestIntervalSeconds,
            configuration.GetValue(
                "Http:MinimumRequestIntervalSeconds",
                DefaultRequestIntervalSeconds));
        services.AddSingleton(
            new HostRateLimiter(TimeSpan.FromSeconds(requestIntervalSeconds)));
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
        services.AddScoped<IBoxScoreRepository, BoxScoreRepository>();
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
        services.AddScoped<LeagueProjectionService>();
        services.AddSingleton(new FantasyBasketball.Domain.Trends.PlayerHeatOptions());
        services.AddScoped<FantasyBasketball.Domain.Trends.PlayerHeatCalculator>();
        services.AddScoped<FantasyBasketball.Application.Trends.PlayerPerformanceService>();
        services.AddSingleton<FantasyBasketball.Domain.Scoring.PointsScoringEngine>();
        services.AddScoped<ProjectionDecompositionService>();
        services.AddScoped<ContextEventService>();
        services.AddScoped<ContextEventQueryService>();
        services.AddScoped<DraftSessionService>();
        services.AddScoped<DraftBoardService>();
        services.AddScoped<LeagueService>();
        services.AddScoped<PlayerQueryService>();
        services.AddScoped<DataSourceHealthService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<AccountDataService>();
        services.AddScoped<OwnedResourceAuthorizationService>();
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
        // The queue itself always runs -- it is what executes a manual import.
        // The three RECURRING refreshers are switchable; see
        // RefreshWorkerOptions.Enabled for why that matters on a small rate
        // budget. Default true, so this changes nothing unless asked.
        if (configuration
            .GetSection(RefreshWorkerOptions.SectionName)
            .GetValue("Enabled", true))
        {
            services.AddHostedService<ScheduleRefreshWorker>();
            services.AddHostedService<StatRefreshWorker>();
            services.AddHostedService<AdpRefreshWorker>();
        }

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
        // The defaults allow 10s per attempt, and balldontlie's free tier
        // regularly takes ~9s to answer a players page. Every import therefore
        // timed out, retried, and recorded a failed run against a working API
        // key -- the data source looked broken when it was only slow.
        //
        // The handler validates these against each other: the total must be at
        // least twice an attempt, and the breaker's sampling window likewise,
        // so all three move together.
        clientBuilder.AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        });
        clientBuilder.AddHttpMessageHandler(serviceProvider =>
            new HostRateLimitHandler(
                serviceProvider.GetRequiredService<HostRateLimiter>()));
    }
}
