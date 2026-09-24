using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

/// <summary>Refreshes the injury report daily.</summary>
public sealed class AvailabilityRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<AvailabilityRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly AvailabilityRefreshOptions options = options.Value.Availability;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest CreateRequest(DateTimeOffset now) => new(ImportJobKind.Availability);
}
