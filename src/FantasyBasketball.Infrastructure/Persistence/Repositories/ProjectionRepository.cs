using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class ProjectionRepository(FantasyDbContext database)
    : IProjectionRepository
{
    public async Task AddAsync(
        ObservedStats observed,
        BaselineProjection baseline,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(baseline);
        if (observed.PlayerId != baseline.PlayerId)
        {
            throw new ArgumentException(
                "Observed stats and baseline must belong to the same player.",
                nameof(baseline));
        }

        database.ObservedStats.Add(ObservedStatsRow.Create(
            observed.PlayerId.Value,
            observed.Source.SeasonEndYear,
            observed.Source.Provenance.Source,
            observed.AsOf));
        database.BaselineProjections.Add(BaselineProjectionRow.Create(
            baseline.Id,
            baseline.PlayerId.Value,
            Round(baseline.ProjectedMinutesPerGame),
            Serialize(baseline.PerMinuteRates),
            Serialize(baseline.ProjectedPerGame),
            baseline.ProjectedGamesPlayed,
            baseline.ComputedAt,
            baseline.ModelVersion));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<ObservedStats?> GetLatestObservedAsync(
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        var observed = await database.ObservedStats
            .AsNoTracking()
            .Where(row => row.PlayerId == playerId.Value)
            .OrderByDescending(row => row.AsOf)
            .ThenByDescending(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (observed is null)
        {
            return null;
        }

        var source = await database.SeasonStatLines
            .AsNoTracking()
            .SingleAsync(
                row => row.PlayerId == observed.PlayerId
                    && row.SeasonEndYear == observed.SeasonEndYear
                    && row.Source == observed.Source,
                cancellationToken);
        return new ObservedStats(
            playerId,
            Map(source),
            observed.AsOf);
    }

    public async Task<BaselineProjection?> GetBaselineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.BaselineProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return row is null
            ? null
            : new BaselineProjection(
                row.Id,
                new PlayerId(row.PlayerId),
                row.ProjectedMinutesPerGame,
                Deserialize(row.PerMinuteRates),
                Deserialize(row.ProjectedPerGame),
                row.ProjectedGamesPlayed,
                row.ComputedAt,
                row.ModelVersion);
    }

    private static SeasonStatLine Map(SeasonStatLineRow row) =>
        new(
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

    private static string Serialize(StatLine line) =>
        JsonSerializer.Serialize(line.Values.ToDictionary(
            entry => entry.Key.ToString(),
            entry => Round(entry.Value)));

    private static StatLine Deserialize(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
            ?? throw new InvalidOperationException("Persisted StatLine JSON was null.");
        return new StatLine(values.ToDictionary(
            entry => Enum.Parse<StatKey>(entry.Key),
            entry => entry.Value));
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
