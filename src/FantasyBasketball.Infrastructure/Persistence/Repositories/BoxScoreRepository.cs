using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Trends;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class BoxScoreRepository(FantasyDbContext database) : IBoxScoreRepository
{
    public async Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken cancellationToken)
    {
        var latest = await database.BoxScoreSnapshots.AsNoTracking()
            .GroupBy(row => new { row.SeasonEndYear, row.Source, row.GameId })
            .Select(group => group.OrderByDescending(row => row.FetchedAt).ThenByDescending(row => row.Id).First())
            .ToArrayAsync(cancellationToken);
        return latest.Where(row => row.Phase == NbaGamePhase.RegularSeason.ToString())
            .GroupBy(row => new { row.SeasonEndYear, row.Source })
            .Select(group => new BoxScorePool(group.Key.SeasonEndYear, group.Key.Source, group.Count(),
                group.Max(row => row.PlayedOn), group.Max(row => row.FetchedAt)))
            .OrderByDescending(pool => pool.SeasonEndYear).ThenBy(pool => pool.Source).ToArray();
    }

    public async Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var provenance = snapshot.Provenance;
        var phase = snapshot.Phase.ToString();
        if (!await database.NbaGames.AsNoTracking().AnyAsync(
            game => game.Id == snapshot.GameId && game.SeasonEndYear == snapshot.SeasonEndYear, cancellationToken))
        {
            throw new ArgumentException("The box score must reference an imported game in the same season.", nameof(snapshot));
        }

        if (await database.BoxScoreSnapshots.AsNoTracking().AnyAsync(row => row.GameId == snapshot.GameId
            && row.Source == provenance.Source && row.ParserVersion == provenance.ParserVersion
            && row.RawRecordHash == provenance.RawRecordHash && row.Phase == phase, cancellationToken))
        {
            return false;
        }

        var row = BoxScoreSnapshotRow.Create(Guid.NewGuid(), snapshot);
        var statistics = snapshot.Samples.Select(sample => PlayerGameStatRow.Create(row.Id, sample,
            sample.Statistics is null ? null : Serialize(sample.Statistics))).ToArray();
        database.BoxScoreSnapshots.Add(row);
        database.PlayerGameStats.AddRange(statistics);
        try
        {
            // EF publishes this graph in one transaction, including every player row.
            await database.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_box_score_snapshot_content" })
        {
            DetachAttempt(row, statistics);
            return false;
        }
        catch
        {
            DetachAttempt(row, statistics);
            throw;
        }
    }

    public async Task<IReadOnlyList<PlayerGameSample>> ListAsync(
        int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken)
    {
        if (seasonEndYear < 1947 || !DataSourceName.IsKnown(source) || !Enum.IsDefined(phase))
            throw new ArgumentException("Select a valid season, source and game phase.");
        // Choose corrections before applying date/phase filters, never resurrect an older version.
        var latest = await database.BoxScoreSnapshots.AsNoTracking()
            .Where(row => row.SeasonEndYear == seasonEndYear && row.Source == source)
            .GroupBy(row => row.GameId)
            .Select(group => group.OrderByDescending(row => row.FetchedAt).ThenByDescending(row => row.Id).First())
            .ToArrayAsync(cancellationToken);
        var selected = latest.Where(row => row.Phase == phase.ToString() && row.PlayedOn <= throughDate)
            .ToDictionary(row => row.Id);
        if (selected.Count == 0) return [];
        var ids = selected.Keys.ToArray();
        var rows = await database.PlayerGameStats.AsNoTracking()
            .Where(row => ids.Contains(row.SnapshotId)).ToArrayAsync(cancellationToken);
        return rows.Select(row =>
        {
            var snapshot = selected[row.SnapshotId];
            return new PlayerGameSample(snapshot.GameId, new PlayerId(row.PlayerId), snapshot.SeasonEndYear,
                snapshot.PlayedOn, true, row.DidPlay, row.Statistics is null ? null : Deserialize(row.Statistics),
                new DataProvenance(row.Source, row.ExternalId, row.FetchedAt, row.SourceTimestamp,
                    row.ParserVersion, row.Confidence, row.RawRecordHash));
        }).OrderBy(sample => sample.PlayedOn).ThenBy(sample => sample.GameId)
            .ThenBy(sample => sample.PlayerId.Value).ToArray();
    }

    private void DetachAttempt(BoxScoreSnapshotRow row, IEnumerable<PlayerGameStatRow> statistics)
    {
        foreach (var item in statistics) database.Entry(item).State = EntityState.Detached;
        database.Entry(row).State = EntityState.Detached;
    }

    private static string Serialize(StatLine line) => JsonSerializer.Serialize(line.Values.ToDictionary(
        entry => entry.Key.ToString(), entry => decimal.Round(entry.Value, 4, MidpointRounding.AwayFromZero)));

    private static StatLine Deserialize(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
            ?? throw new InvalidDataException("Persisted game statistics are missing.");
        return new StatLine(values.ToDictionary(entry => Enum.Parse<StatKey>(entry.Key), entry => entry.Value));
    }
}
