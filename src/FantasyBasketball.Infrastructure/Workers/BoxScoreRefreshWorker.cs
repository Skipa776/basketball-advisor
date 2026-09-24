using FantasyBasketball.Application.Ingestion;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FantasyBasketball.Infrastructure.Workers;

/// <summary>
/// Nightly regular-season box scores. Off unless the owner enables it and names the
/// season's regular-season dates: the stored schedule cannot tell preseason, play-in
/// or playoff games apart, so the dates are the phase guard.
/// </summary>
public sealed class BoxScoreRefreshWorker(
    IImportJobQueue jobs,
    TimeProvider timeProvider,
    IOptions<RefreshWorkerOptions> options,
    ILogger<BoxScoreRefreshWorker> logger)
    : RecurringImportWorker(jobs, timeProvider, logger)
{
    private readonly BoxScoreRefreshOptions options = options.Value.BoxScores;

    protected override TimeSpan StartupDelay => options.StartupDelay;

    protected override TimeSpan Cadence => options.Cadence;

    protected override ImportJobRequest? CreateRequest(DateTimeOffset now) =>
        RequestFor(DateOnly.FromDateTime(now.UtcDateTime), options);

    /// <summary>The last few completed days inside the regular season, after any excluded date.</summary>
    public static ImportJobRequest? RequestFor(DateOnly today, BoxScoreRefreshOptions options)
    {
        if (!options.Enabled || options.RegularSeasonStart is not { } start || options.RegularSeasonEnd is not { } end)
        {
            return null;
        }

        var from = Max(start, today.AddDays(-options.LookbackDays));
        var to = Min(end, today.AddDays(-1));
        // ponytail: an excluded date inside the window moves its start past it; earlier days were covered by earlier nights.
        foreach (var excluded in options.ExcludedDates.Where(date => date >= from && date <= to))
        {
            from = Max(from, excluded.AddDays(1));
        }

        return from > to ? null : new ImportJobRequest(ImportJobKind.BoxScores, From: from, To: to);
    }

    private static DateOnly Max(DateOnly left, DateOnly right) => left > right ? left : right;

    private static DateOnly Min(DateOnly left, DateOnly right) => left < right ? left : right;
}
