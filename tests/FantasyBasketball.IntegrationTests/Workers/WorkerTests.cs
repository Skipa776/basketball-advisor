using System.Collections.Concurrent;
using System.Diagnostics;
using FantasyBasketball.Api;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;
using ExternalAdpEntry = FantasyBasketball.Application.Abstractions.AdpEntry;

namespace FantasyBasketball.IntegrationTests.Workers;

public sealed class WorkerTests
{
    [Fact]
    public async Task W01_W03_failed_run_is_recorded_next_runs_continue_with_fresh_scopes()
    {
        var probe = new WorkerProbe();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(probe);
        services.AddScoped<IAdpProvider, ProbeAdpProvider>();
        services.AddScoped<IPlayerRepository, EmptyPlayerRepository>();
        services.AddScoped<IAdpRepository, EmptyAdpRepository>();
        services.AddScoped<IDataImportRunRepository>(
            serviceProvider => serviceProvider.GetRequiredService<WorkerProbe>());
        services.AddScoped<IImportTransaction, ProbeImportTransaction>();
        services.AddScoped<PlayerIdentityResolver>();
        services.AddScoped<ImportAdpService>();
        await using var provider = services.BuildServiceProvider();
        var queue = ActivatorUtilities.CreateInstance<ImportJobQueue>(provider);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await queue.StartAsync(timeout.Token);
        var first = await queue.EnqueueAsync(
            new ImportJobRequest(ImportJobKind.Adp),
            timeout.Token);
        var second = await queue.EnqueueAsync(
            new ImportJobRequest(ImportJobKind.Adp),
            timeout.Token);
        var third = await queue.EnqueueAsync(
            new ImportJobRequest(ImportJobKind.Adp),
            timeout.Token);
        await probe.ThreeRuns.Task.WaitAsync(timeout.Token);
        await queue.StopAsync(timeout.Token);
        queue.Dispose();

        probe.Runs.Select(run => run.Id).ShouldBe([first.Id, second.Id, third.Id]);
        probe.Runs.Select(run => run.Status).ShouldBe(
        [
            DataImportRunStatus.Failed,
            DataImportRunStatus.Succeeded,
            DataImportRunStatus.Succeeded,
        ]);
        probe.ProviderScopes.Distinct().Count().ShouldBe(3);
        probe.TransactionScopes.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task W02_host_shutdown_cancels_a_staggered_worker_promptly()
    {
        var options = Options.Create(new RefreshWorkerOptions
        {
            Schedule = new ScheduleRefreshOptions
            {
                StartupDelay = TimeSpan.FromDays(1),
                Cadence = TimeSpan.FromDays(1),
            },
        });
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        var worker = new ScheduleRefreshWorker(
            new RecordingJobQueue(),
            TimeProvider.System,
            options,
            provider.GetRequiredService<
                Microsoft.Extensions.Logging.ILogger<ScheduleRefreshWorker>>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(timeout.Token);
        var stopwatch = Stopwatch.StartNew();
        await worker.StopAsync(timeout.Token);
        stopwatch.Stop();
        worker.Dispose();

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Worker_cadences_bind_from_configuration_and_all_three_are_registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["BallDontLie:ApiKey"] = "fixture-api-key",
                ["ConnectionStrings:Fantasy"] =
                    "Host=localhost;Database=fixture;Username=fixture;Password=fixture",
                ["RefreshWorkers:Schedule:Cadence"] = "00:42:00",
                ["RefreshWorkers:Stats:Cadence"] = "01:13:00",
                ["RefreshWorkers:Adp:Cadence"] = "02:17:00",
            });

        ApiHost.ConfigureServices(builder);

        var hostedTypes = builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .Select(descriptor => descriptor.ImplementationType)
            .Where(type => type is not null)
            .ToArray();
        hostedTypes.ShouldContain(typeof(ScheduleRefreshWorker));
        hostedTypes.ShouldContain(typeof(StatRefreshWorker));
        hostedTypes.ShouldContain(typeof(AdpRefreshWorker));

        using var provider = builder.Services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RefreshWorkerOptions>>()
            .Value;
        options.Schedule.Cadence.ShouldBe(TimeSpan.FromMinutes(42));
        options.Stats.Cadence.ShouldBe(TimeSpan.FromMinutes(73));
        options.Adp.Cadence.ShouldBe(TimeSpan.FromMinutes(137));
    }

    private sealed class WorkerProbe :
        IDataImportRunRepository
    {
        private readonly ConcurrentQueue<DataImportRun> runs = new();
        private readonly ConcurrentQueue<Guid> providerScopes = new();
        private readonly ConcurrentQueue<Guid> transactionScopes = new();

        public TaskCompletionSource ThreeRuns { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<DataImportRun> Runs => runs.ToArray();

        public IReadOnlyList<Guid> ProviderScopes => providerScopes.ToArray();

        public IReadOnlyList<Guid> TransactionScopes =>
            transactionScopes.ToArray();

        public void AddProviderScope(Guid id) => providerScopes.Enqueue(id);

        public void AddTransactionScope(Guid id) =>
            transactionScopes.Enqueue(id);

        public Task AddAsync(
            DataImportRun run,
            CancellationToken cancellationToken)
        {
            runs.Enqueue(run);
            if (runs.Count == 3)
            {
                ThreeRuns.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public Task<DataImportRun?> GetAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(runs.SingleOrDefault(run => run.Id == id));
    }

    private sealed class ProbeAdpProvider : IAdpProvider
    {
        private static int calls;
        private readonly WorkerProbe probe;

        public ProbeAdpProvider(WorkerProbe probe)
        {
            this.probe = probe;
            probe.AddProviderScope(Guid.NewGuid());
        }

        public string Name => DataSourceName.FantasyPros;

        public DataSourceKind Kind => DataSourceKind.Scraper;

        public Task<IReadOnlyList<ExternalAdpEntry>> GetAdpAsync(
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("fixture failure");
            }

            return Task.FromResult<IReadOnlyList<ExternalAdpEntry>>([]);
        }
    }

    private sealed class ProbeImportTransaction : IImportTransaction
    {
        private readonly WorkerProbe probe;
        private readonly Guid id = Guid.NewGuid();

        public ProbeImportTransaction(WorkerProbe probe)
        {
            this.probe = probe;
            probe.AddTransactionScope(id);
        }

        public Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            action(cancellationToken);
    }

    private sealed class EmptyPlayerRepository : IPlayerRepository
    {
        public Task AddAsync(
            Player player,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Player?> GetAsync(
            PlayerId id,
            CancellationToken cancellationToken) =>
            Task.FromResult<Player?>(null);

        public Task<Player?> FindByExternalIdentityAsync(
            string provider,
            string externalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Player?>(null);

        public Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
            string normalizedName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Player>>([]);

        public Task<ExternalPlayerIdentity?> FindIdentityAsync(
            PlayerId playerId,
            string provider,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExternalPlayerIdentity?>(null);

        public Task AddResolvedIdentityAsync(
            Player player,
            ExternalPlayerIdentity identity,
            bool addPlayer,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AddPendingIdentityMatchAsync(
            PendingIdentityMatch pendingMatch,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class EmptyAdpRepository : IAdpRepository
    {
        public Task AddAsync(
            CanonicalAdpEntry entry,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<CanonicalAdpEntry?> GetLatestAsync(
            PlayerId playerId,
            CancellationToken cancellationToken) =>
            Task.FromResult<CanonicalAdpEntry?>(null);
    }

    private sealed class RecordingJobQueue : IImportJobQueue
    {
        public Task<DataImportRun> EnqueueAsync(
            ImportJobRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DataImportRun(
                Guid.NewGuid(),
                DataSourceName.BallDontLie,
                DataImportRunStatus.Running,
                DateTimeOffset.UnixEpoch,
                null,
                0,
                0,
                null));

        public IReadOnlyList<DataImportRun> GetActive() => [];
    }
}
