using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

/// <summary>
/// Refreshes the current injury report. Players are matched by the source's identity, then by a
/// unique name; nobody is created from an injury list, and unmatched names are counted, not guessed.
/// </summary>
public sealed class ImportAvailabilityService(
    IPlayerRepository players,
    IAvailabilityRepository availability,
    IDataImportRunRepository runs,
    TimeProvider clock)
{
    public async Task<DataImportRun> ImportFromAsync(IAvailabilitySource source, Guid? runId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var startedAt = clock.GetUtcNow();
        try
        {
            var reports = new Dictionary<PlayerId, PlayerAvailability>();
            var unmatched = 0;
            foreach (var injury in await source.GetInjuriesAsync(cancellationToken))
            {
                if (PlayerAvailability.ParseStatus(injury.StatusCode) is not { } status)
                {
                    continue;
                }

                var player = await players.FindByExternalIdentityAsync(source.Name, injury.ExternalPlayerId, cancellationToken);
                if (player is null)
                {
                    var byName = await players.FindByNormalizedNameAsync(PlayerName.Normalize(injury.FullName), cancellationToken);
                    player = byName.Count == 1 ? byName[0] : null;
                }

                if (player is null)
                {
                    unmatched++;
                    continue;
                }

                reports[player.Id] = new PlayerAvailability(player.Id, status, injury.BodyPart, injury.Notes, injury.ReportedAt, injury.Provenance);
            }

            await availability.ReplaceAsync(source.Name, reports.Values.ToArray(), cancellationToken);
            var run = new DataImportRun(runId ?? Guid.NewGuid(), source.Name, DataImportRunStatus.Succeeded, startedAt,
                clock.GetUtcNow(), reports.Count, unmatched, null);
            await runs.AddAsync(run, cancellationToken);
            return run;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failed = FailedRun.Create(runId, source.Name, startedAt, clock.GetUtcNow(), exception);
            await runs.AddAsync(failed, cancellationToken);
            return failed;
        }
    }
}
