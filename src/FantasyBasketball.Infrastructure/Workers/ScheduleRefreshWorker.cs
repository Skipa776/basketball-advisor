using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

public sealed class ScheduleRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<ScheduleRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly ScheduleRefreshOptions options = options.Value.Schedule;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest CreateRequest(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        return new ImportJobRequest(
            ImportJobKind.Schedule,
            From: today.AddDays(-options.LookbackDays),
            To: today.AddDays(options.LookAheadDays));
    }
}
