using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FantasyBasketball.Infrastructure.Workers;

public abstract class RecurringImportWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    ILogger logger)
    : BackgroundService
{
    protected abstract TimeSpan StartupDelay { get; }

    protected abstract TimeSpan Cadence { get; }

    protected abstract ImportJobRequest? CreateRequest(DateTimeOffset now);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, timeProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = CreateRequest(timeProvider.GetUtcNow());
                if (request is not null)
                {
                    var run = await jobs.EnqueueAsync(request, stoppingToken);
                    logger.LogInformation(
                        "Scheduled import run {RunId} for {ImportKind}",
                        run.Id,
                        request.Kind);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "Scheduled import enqueue failed with {ExceptionType}",
                    exception.GetType().Name);
            }

            try
            {
                await Task.Delay(Cadence, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
