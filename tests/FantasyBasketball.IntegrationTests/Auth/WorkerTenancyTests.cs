using System.Collections.Concurrent;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Accounts;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Identity;
using FantasyBasketball.Infrastructure.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace FantasyBasketball.IntegrationTests.Auth;

public sealed class WorkerTenancyTests
{
    [Fact]
    public async Task U11_every_background_worker_runs_with_throwing_user_context()
    {
        var queue = new RecordingQueue();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IUserContext>(MissingUserContext.Instance);
        services.AddSingleton<IImportJobQueue>(queue);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Options.Create(new RefreshWorkerOptions
        {
            Schedule = new ScheduleRefreshOptions
            {
                StartupDelay = TimeSpan.Zero,
                Cadence = TimeSpan.FromDays(1),
            },
            Stats = new StatRefreshOptions
            {
                StartupDelay = TimeSpan.Zero,
                Cadence = TimeSpan.FromDays(1),
            },
            Adp = new AdpRefreshOptions
            {
                StartupDelay = TimeSpan.Zero,
                Cadence = TimeSpan.FromDays(1),
                ActiveFromMonth = 1,
                ActiveThroughMonth = 12,
            },
        }));
        services.AddSingleton<ScheduleRefreshWorker>();
        services.AddSingleton<StatRefreshWorker>();
        services.AddSingleton<AdpRefreshWorker>();
        await using var provider = services.BuildServiceProvider();
        var workers = new IHostedService[]
        {
            provider.GetRequiredService<ScheduleRefreshWorker>(),
            provider.GetRequiredService<StatRefreshWorker>(),
            provider.GetRequiredService<AdpRefreshWorker>(),
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        foreach (var worker in workers)
        {
            await worker.StartAsync(timeout.Token);
        }

        await queue.AllWorkersRan.Task.WaitAsync(timeout.Token);
        foreach (var worker in workers)
        {
            await worker.StopAsync(timeout.Token);
        }

        queue.Kinds.ShouldBe(
        [
            ImportJobKind.Adp,
            ImportJobKind.Schedule,
            ImportJobKind.SeasonStats,
        ], ignoreOrder: true);
        Should.Throw<InvalidOperationException>(() =>
            provider.GetRequiredService<IUserContext>().CurrentUserId);
    }

    private sealed class RecordingQueue : IImportJobQueue
    {
        private readonly ConcurrentDictionary<ImportJobKind, byte> kinds = [];

        public TaskCompletionSource AllWorkersRan { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ImportJobKind[] Kinds => kinds.Keys.ToArray();

        public Task<DataImportRun> EnqueueAsync(
            ImportJobRequest request,
            CancellationToken cancellationToken)
        {
            kinds.TryAdd(request.Kind, 0);
            if (kinds.Count == 3)
            {
                AllWorkersRan.TrySetResult();
            }

            return Task.FromResult(new DataImportRun(
                Guid.NewGuid(),
                DataSourceName.Manual,
                DataImportRunStatus.Running,
                DateTimeOffset.UnixEpoch,
                null,
                0,
                0,
                null));
        }

        public IReadOnlyList<DataImportRun> GetActive() => [];
    }
}
