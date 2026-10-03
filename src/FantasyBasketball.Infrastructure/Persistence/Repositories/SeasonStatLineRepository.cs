using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class SeasonStatLineRepository(FantasyDbContext database)
    : ISeasonStatLineRepository
{
    public async Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(
        CancellationToken cancellationToken) =>
        await database.SeasonStatLines.AsNoTracking()
            .GroupBy(row => new { row.SeasonEndYear, row.Source })
            .OrderByDescending(group => group.Key.SeasonEndYear).ThenBy(group => group.Key.Source)
            .Select(group => new SeasonProjectionPool(
                group.Key.SeasonEndYear, group.Key.Source, group.Count()))
            .Take(Paging.MaximumLimit).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(
        int seasonEndYear, string source, CancellationToken cancellationToken)
    {
        var rows = await database.SeasonStatLines.AsNoTracking()
            .Where(row => row.SeasonEndYear == seasonEndYear
                && (row.Source == source || row.Source == DataSourceName.Manual))
            .ToArrayAsync(cancellationToken);
        if (!rows.Any(row => row.Source == source))
        {
            return [];
        }

        return rows.GroupBy(row => row.PlayerId)
            .Select(group => group.OrderByDescending(row => row.Source == DataSourceName.Manual).First())
            .OrderBy(row => row.PlayerId).Select(Map).ToArray();
    }

    public async Task AddAsync(
        SeasonStatLine statLine,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statLine);
        var provenance = statLine.Provenance;

        database.SeasonStatLines.Add(SeasonStatLineRow.Create(
            statLine.PlayerId.Value,
            statLine.SeasonEndYear,
            statLine.GamesPlayed,
            statLine.MinutesPerGame,
            Serialize(statLine.PerGame),
            Serialize(statLine.Totals),
            statLine.UsageRate,
            provenance.Source,
            provenance.ExternalId,
            provenance.FetchedAt,
            provenance.SourceTimestamp,
            provenance.ParserVersion,
            provenance.Confidence,
            provenance.RawRecordHash,
            statLine.Age));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAgeAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        int age,
        CancellationToken cancellationToken) =>
        await database.SeasonStatLines
            .Where(row => row.PlayerId == playerId.Value
                && row.SeasonEndYear == seasonEndYear
                && row.Source == source)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.Age, age), cancellationToken);

    public async Task<SeasonStatLine?> GetAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        CancellationToken cancellationToken)
    {
        var row = await database.SeasonStatLines
            .AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.PlayerId == playerId.Value
                    && value.SeasonEndYear == seasonEndYear
                    && value.Source == source,
                cancellationToken);

        return row is null ? null : Map(row);
    }

    private static SeasonStatLine Map(SeasonStatLineRow row) => new(
                new PlayerId(row.PlayerId),
                row.SeasonEndYear,
                row.GamesPlayed,
                row.MinutesPerGame,
                Deserialize(row.PerGame),
                Deserialize(row.Totals),
                row.UsageRate,
                new DataProvenance(
                    row.Source,
                    row.ExternalId,
                    row.FetchedAt,
                    row.SourceTimestamp,
                    row.ParserVersion,
                    row.Confidence,
                    row.RawRecordHash),
                row.Age);

    private static string Serialize(StatLine line) =>
        JsonSerializer.Serialize(
            line.Values.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value));

    private static StatLine Deserialize(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
            ?? throw new InvalidOperationException("Persisted StatLine JSON was null.");
        return new StatLine(values.ToDictionary(
            entry => Enum.Parse<StatKey>(entry.Key),
            entry => entry.Value));
    }
}
