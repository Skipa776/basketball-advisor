using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class SeasonStatLineRepository(FantasyDbContext database)
    : ISeasonStatLineRepository
{
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
            provenance.RawRecordHash));
        await database.SaveChangesAsync(cancellationToken);
    }

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

        return row is null
            ? null
            : new SeasonStatLine(
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
                    row.RawRecordHash));
    }

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
