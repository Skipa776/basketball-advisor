using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportScheduleService(
    IGameRepository games,
    IDataImportRunRepository runs,
    IImportTransaction transaction,
    TimeProvider timeProvider)
{
    public async Task<DataImportRun> ImportFromAsync(
        IScheduleProvider provider,
        DateOnly from,
        DateOnly to,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var startedAt = timeProvider.GetUtcNow();
        try
        {
            var imported = await provider.GetGamesAsync(
                from,
                to,
                cancellationToken);
            DataImportRun? completed = null;
            await transaction.ExecuteAsync(
                async token =>
                {
                    var written = 0;
                    foreach (var game in imported)
                    {
                        token.ThrowIfCancellationRequested();
                        var externalId = game.Provenance.ExternalId
                            ?? throw new InvalidOperationException(
                                "Schedule provenance requires an external id.");
                        // Games are first seen days ahead as "Scheduled"; later runs must
                        // move them to "Final" or the box-score import never sees them.
                        var stored = await games.GetBySourceAsync(provider.Name, externalId, token);
                        if (stored is not null)
                        {
                            if (stored.Status != game.Status || stored.HomeScore != game.HomeScore
                                || stored.AwayScore != game.AwayScore || stored.StartsAt != game.StartsAt)
                            {
                                await games.SaveResultAsync(game, token);
                                written++;
                            }

                            continue;
                        }

                        await games.AddAsync(game, token);
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
                    "The schedule import transaction did not complete.");
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
