using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

public sealed class AdpRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<AdpRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly AdpRefreshOptions options = options.Value.Adp;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest? CreateRequest(DateTimeOffset now) =>
        now.Month >= options.ActiveFromMonth
        && now.Month <= options.ActiveThroughMonth
            ? new ImportJobRequest(ImportJobKind.Adp)
            : null;
}
