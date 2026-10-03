using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportSeasonStatsService(
    ISeasonStatLineRepository stats,
    IDataImportRunRepository runs,
    IImportTransaction transaction,
    TimeProvider timeProvider)
{
    public async Task<DataImportRun> ImportFromAsync(
        IPlayerStatsProvider provider,
        int seasonEndYear,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var startedAt = timeProvider.GetUtcNow();
        try
        {
            var imported = await provider.GetSeasonStatsAsync(
                seasonEndYear,
                cancellationToken);
            DataImportRun? completed = null;
            await transaction.ExecuteAsync(
                async token =>
                {
                    var written = 0;
                    foreach (var statLine in imported)
                    {
                        token.ThrowIfCancellationRequested();
                        var existing = await stats.GetAsync(
                            statLine.PlayerId,
                            statLine.SeasonEndYear,
                            provider.Name,
                            token);
                        if (existing is not null)
                        {
                            // Lines stay immutable; only a missing age is filled in.
                            if (existing.Age is null && statLine.Age is { } age)
                            {
                                await stats.SaveAgeAsync(statLine.PlayerId, statLine.SeasonEndYear, provider.Name, age, token);
                            }

                            continue;
                        }

                        await stats.AddAsync(statLine, token);
                        written++;
                    }

                    completed = new DataImportRun(
                        runId ?? Guid.NewGuid(),
                        provider.Name,
                        DataImportRunStatus.Succeeded,
                        startedAt,
                        timeProvider.GetUtcNow(),
                        written,
                        0,
                        null);
                    await runs.AddAsync(completed, token);
                },
                cancellationToken);
            return completed
                ?? throw new InvalidOperationException(
                    "The stat import transaction did not complete.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failed = FailedRun.Create(
                runId,
                provider.Name,
                startedAt,
                timeProvider.GetUtcNow(),
                exception);
            await runs.AddAsync(failed, cancellationToken);
            return failed;
        }
    }
}
