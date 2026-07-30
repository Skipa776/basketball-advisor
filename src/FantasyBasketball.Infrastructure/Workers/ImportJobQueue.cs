using System.Collections.Concurrent;
using System.Threading.Channels;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Ingestion;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Import;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FantasyBasketball.Infrastructure.Workers;

public sealed class ImportJobQueue(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ImportJobQueue> logger)
    : BackgroundService, IImportJobQueue
{
    private readonly Channel<QueuedImport> channel =
        Channel.CreateUnbounded<QueuedImport>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
    private readonly ConcurrentDictionary<Guid, DataImportRun> active = new();

    public async Task<DataImportRun> EnqueueAsync(
        ImportJobRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        var run = new DataImportRun(
            Guid.NewGuid(),
            SourceFor(request),
            DataImportRunStatus.Running,
            timeProvider.GetUtcNow(),
            null,
            0,
            0,
            null);
        active[run.Id] = run;
        await channel.Writer.WriteAsync(
            new QueuedImport(run, request),
            cancellationToken);
        return run;
    }

    public IReadOnlyList<DataImportRun> GetActive() =>
        active.Values.OrderByDescending(value => value.StartedAt).ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var queued in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ExecuteJobAsync(queued, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Import job {RunId} failed with {ExceptionType}",
                    queued.Run.Id,
                    exception.GetType().Name);
                await RecordUnexpectedFailureAsync(queued, exception, stoppingToken);
            }
            finally
            {
                active.TryRemove(queued.Run.Id, out _);
            }
        }
    }

    private async Task ExecuteJobAsync(
        QueuedImport queued,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        switch (queued.Request.Kind)
        {
            case ImportJobKind.Players:
                await services.GetRequiredService<ImportDirectoryService>()
                    .ImportFromAsync(
                        services.GetRequiredService<IPlayerDirectoryProvider>(),
                        queued.Run.Id,
                        cancellationToken);
                break;
            case ImportJobKind.Schedule:
                await services.GetRequiredService<ImportScheduleService>()
                    .ImportFromAsync(
                        services.GetRequiredService<IScheduleProvider>(),
                        queued.Request.From!.Value,
                        queued.Request.To!.Value,
                        queued.Run.Id,
                        cancellationToken);
                break;
            case ImportJobKind.SeasonStats:
                await services.GetRequiredService<ImportSeasonStatsService>()
                    .ImportFromAsync(
                        services.GetRequiredService<IPlayerStatsProvider>(),
                        queued.Request.SeasonEndYear!.Value,
                        queued.Run.Id,
                        cancellationToken);
                break;
            case ImportJobKind.Adp:
                var provider = queued.Request.Csv is { } csv
                    ? new CsvAdpImporter(csv, timeProvider)
                    : services.GetRequiredService<IAdpProvider>();
                await services.GetRequiredService<ImportAdpService>()
                    .ImportFromAsync(
                        provider,
                        queued.Run.Id,
                        cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(queued));
        }
    }

    private async Task RecordUnexpectedFailureAsync(
        QueuedImport queued,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository =
            scope.ServiceProvider.GetRequiredService<IDataImportRunRepository>();
        await repository.AddAsync(
            new DataImportRun(
                queued.Run.Id,
                queued.Run.Source,
                DataImportRunStatus.Failed,
                queued.Run.StartedAt,
                timeProvider.GetUtcNow(),
                0,
                0,
                $"{exception.GetType().Name}: queued import failed."),
            cancellationToken);
    }

    private static void Validate(ImportJobRequest request)
    {
        if (request.Kind == ImportJobKind.Schedule
            && (request.From is null
                || request.To is null
                || request.To < request.From))
        {
            throw new ArgumentException(
                "Schedule imports require a valid date range.",
                nameof(request));
        }

        if (request.Kind == ImportJobKind.SeasonStats
            && request.SeasonEndYear is not (> 1946 and < 2200))
        {
            throw new ArgumentException(
                "Season end year is invalid.",
                nameof(request));
        }
    }

    private static string SourceFor(ImportJobRequest request) =>
        request.Kind switch
        {
            ImportJobKind.Players or ImportJobKind.Schedule =>
                DataSourceName.BallDontLie,
            ImportJobKind.SeasonStats => DataSourceName.BasketballReference,
            ImportJobKind.Adp when request.Csv is not null => DataSourceName.Csv,
            ImportJobKind.Adp => DataSourceName.FantasyPros,
            _ => throw new ArgumentOutOfRangeException(nameof(request)),
        };

    private sealed record QueuedImport(
        DataImportRun Run,
        ImportJobRequest Request);
}
