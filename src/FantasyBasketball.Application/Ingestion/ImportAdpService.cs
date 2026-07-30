using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;
using ExternalAdpEntry = FantasyBasketball.Application.Abstractions.AdpEntry;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportAdpService(
    PlayerIdentityResolver resolver,
    IAdpRepository adp,
    IDataImportRunRepository runs,
    IImportTransaction transaction,
    TimeProvider timeProvider)
{
    public async Task<DataImportRun> ImportFromAsync(
        IAdpProvider provider,
        CancellationToken cancellationToken) =>
        await ImportFromAsync(provider, null, cancellationToken);

    public async Task<DataImportRun> ImportFromAsync(
        IAdpProvider provider,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var startedAt = timeProvider.GetUtcNow();

        try
        {
            var entries = await provider.GetAdpAsync(cancellationToken);
            return await ImportAsync(
                provider.Name,
                entries,
                runId,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failedRun = CreateFailedRun(
                provider.Name,
                startedAt,
                exception,
                runId);
            await runs.AddAsync(failedRun, cancellationToken);
            return failedRun;
        }
    }

    public async Task<DataImportRun> ImportAsync(
        string source,
        IReadOnlyList<ExternalAdpEntry> entries,
        CancellationToken cancellationToken) =>
        await ImportAsync(source, entries, null, cancellationToken);

    public async Task<DataImportRun> ImportAsync(
        string source,
        IReadOnlyList<ExternalAdpEntry> entries,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(entries);

        if (!DataSourceName.IsKnown(source))
        {
            throw new ArgumentException(
                "Source is not a canonical data source name.",
                nameof(source));
        }

        if (entries.Any(entry => entry.Provenance.Source != source))
        {
            throw new ArgumentException(
                "Every ADP entry must carry provenance for the import source.",
                nameof(entries));
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

                    foreach (var entry in entries)
                    {
                        token.ThrowIfCancellationRequested();
                        var resolution = await resolver.ResolveAsync(
                            new ExternalPlayer(
                                entry.ExternalId,
                                entry.PlayerName,
                                null,
                                [],
                                null,
                                entry.Provenance),
                            token);
                        if (resolution.Player is null)
                        {
                            pendingIdentityMatches++;
                            continue;
                        }

                        await adp.AddAsync(
                            new CanonicalAdpEntry(
                                Guid.NewGuid(),
                                resolution.Player.Id,
                                entry.AverageDraftPosition,
                                entry.StandardDeviation,
                                entry.Provenance),
                            token);
                        rowsWritten++;
                    }

                    completedRun = new DataImportRun(
                        runId ?? Guid.NewGuid(),
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
            var failedRun = CreateFailedRun(source, startedAt, exception, runId);
            await runs.AddAsync(failedRun, cancellationToken);
            return failedRun;
        }

        return completedRun
            ?? throw new InvalidOperationException("The import transaction did not complete.");
    }

    private DataImportRun CreateFailedRun(
        string source,
        DateTimeOffset startedAt,
        Exception exception,
        Guid? runId) =>
        FailedRun.Create(
            runId,
            source,
            startedAt,
            timeProvider.GetUtcNow(),
            exception);
}
