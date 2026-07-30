using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportDirectoryService(
    ImportPlayersService players,
    ITeamRepository teams,
    IDataImportRunRepository runs,
    TimeProvider timeProvider)
{
    public async Task<DataImportRun> ImportFromAsync(
        IPlayerDirectoryProvider provider,
        Guid? runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var startedAt = timeProvider.GetUtcNow();
        try
        {
            var externalTeams = await provider.GetTeamsAsync(cancellationToken);
            foreach (var external in externalTeams)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await teams.FindByAbbreviationAsync(
                        external.Abbreviation,
                        cancellationToken) is not null)
                {
                    continue;
                }

                await teams.AddAsync(
                    new NbaTeam(
                        new NbaTeamId(Guid.NewGuid()),
                        external.Name,
                        external.Abbreviation),
                    external.Provenance,
                    cancellationToken);
            }

            return await players.ImportFromAsync(
                provider,
                runId,
                cancellationToken);
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
