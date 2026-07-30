using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Health;

public sealed class DataSourceHealthOptions
{
    public const string SectionName = "DataSourceHealth";

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromDays(2);

    public bool IsValid() => StaleAfter > TimeSpan.Zero;
}

public sealed record DataSourceHealth(
    string Source,
    DateTimeOffset? LastSuccess,
    DateTimeOffset? LastFailure,
    bool IsStale,
    bool IsDegraded);

public sealed class DataSourceHealthService(
    IImportRunQueryRepository runs,
    TimeProvider timeProvider,
    DataSourceHealthOptions options)
{
    private static readonly IReadOnlyList<string> Sources =
    [
        DataSourceName.BallDontLie,
        DataSourceName.BasketballReference,
        DataSourceName.FantasyPros,
        DataSourceName.Csv,
        DataSourceName.Manual,
    ];

    public async Task<IReadOnlyList<DataSourceHealth>> GetAsync(
        CancellationToken cancellationToken)
    {
        var recent = await runs.ListRecentAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        return Sources.Select(source =>
        {
            var sourceRuns = recent
                .Where(run => run.Source == source)
                .OrderByDescending(run => run.FinishedAt ?? run.StartedAt)
                .ToArray();
            var lastSuccess = sourceRuns
                .FirstOrDefault(run => run.Status == DataImportRunStatus.Succeeded)
                ?.FinishedAt;
            var lastFailure = sourceRuns
                .FirstOrDefault(run => run.Status == DataImportRunStatus.Failed)
                ?.FinishedAt;
            var isStale = lastSuccess is null
                || now - lastSuccess.Value > options.StaleAfter;
            var isDegraded = isStale
                || (lastFailure is not null
                    && (lastSuccess is null || lastFailure > lastSuccess));
            return new DataSourceHealth(
                source,
                lastSuccess,
                lastFailure,
                isStale,
                isDegraded);
        }).ToArray();
    }
}
