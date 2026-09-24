using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

/// <summary>Weekly player directory refresh, so trades and signings move players to their current team.</summary>
public sealed class PlayerDirectoryRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<PlayerDirectoryRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly PlayerDirectoryRefreshOptions options = options.Value.Players;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest CreateRequest(DateTimeOffset now) => new(ImportJobKind.Players);
}
