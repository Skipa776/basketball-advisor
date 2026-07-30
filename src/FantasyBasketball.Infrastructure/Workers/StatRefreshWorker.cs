using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

public sealed class StatRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<StatRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly StatRefreshOptions options = options.Value.Stats;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest CreateRequest(DateTimeOffset now)
    {
        var seasonEndYear = now.Month >= 7
            ? now.Year + 1
            : now.Year;
        return new ImportJobRequest(
            ImportJobKind.SeasonStats,
            SeasonEndYear: seasonEndYear);
    }
}
