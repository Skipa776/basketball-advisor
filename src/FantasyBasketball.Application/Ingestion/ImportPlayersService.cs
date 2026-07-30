using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportPlayersService(
    PlayerIdentityResolver resolver,
    IDataImportRunRepository runs,
    IImportTransaction transaction,
    TimeProvider timeProvider)
{
    public async Task<DataImportRun> ImportAsync(
        string source,
        IReadOnlyList<ExternalPlayer> externalPlayers,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(externalPlayers);

        if (!DataSourceName.IsKnown(source))
        {
            throw new ArgumentException(
                "Source is not a canonical data source name.",
                nameof(source));
        }

        if (externalPlayers.Any(player => player.Provenance.Source != source))
        {
            throw new ArgumentException(
                "Every imported player must carry provenance for the import source.",
                nameof(externalPlayers));
        }

        var startedAt = timeProvider.GetUtcNow();
        DataImportRun? completedRun = null;

        try
        {
            await transaction.ExecuteAsync(
                async token =>
                {
                    var rowsWritten = 0;
                    var pendingIdentityMatches = 0;

                    foreach (var externalPlayer in externalPlayers)
                    {
                        token.ThrowIfCancellationRequested();
                        var resolution = await resolver.ResolveAsync(externalPlayer, token);
                        if (resolution.PendingMatch is null)
                        {
                            rowsWritten++;
                        }
                        else
                        {
                            pendingIdentityMatches++;
                        }
                    }

                    completedRun = new DataImportRun(
                        Guid.NewGuid(),
                        source,
                        DataImportRunStatus.Succeeded,
                        startedAt,
                        timeProvider.GetUtcNow(),
                        rowsWritten,
                        pendingIdentityMatches,
                        null);
                    await runs.AddAsync(completedRun, token);
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedRun = new DataImportRun(
                Guid.NewGuid(),
                source,
                DataImportRunStatus.Failed,
                startedAt,
                timeProvider.GetUtcNow(),
                0,
                0,
                $"{exception.GetType().Name}: {exception.Message}");
            await runs.AddAsync(failedRun, cancellationToken);
            return failedRun;
        }

        return completedRun
            ?? throw new InvalidOperationException("The import transaction did not complete.");
    }
}
