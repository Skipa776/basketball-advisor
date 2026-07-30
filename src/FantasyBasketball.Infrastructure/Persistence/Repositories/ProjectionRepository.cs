using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class ProjectionRepository(FantasyDbContext database)
    : IProjectionRepository, IProjectionQueryRepository
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

    public async Task AddAdjustedAsync(
        AdjustedProjection adjusted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(adjusted);
        database.AdjustedProjections.Add(AdjustedProjectionRow.Create(
            adjusted.Id,
            database.CurrentUserId,
            adjusted.PlayerId.Value,
            adjusted.BaselineProjectionId,
            Serialize(adjusted.ProjectedPerGame),
            adjusted.AppliedContextEventIds.ToArray(),
            Round(adjusted.RoleRisk),
            adjusted.Confidence.ToString(),
            Round(adjusted.ContextCertainty),
            adjusted.HasUnverifiedContext,
            adjusted.ComputedAt));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdjustedProjection?> GetAdjustedAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.AdjustedProjections
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return row is null
            ? null
            : new AdjustedProjection(
                row.Id,
                new PlayerId(row.PlayerId),
                row.BaselineProjectionId,
                Deserialize(row.ProjectedPerGame),
                row.AppliedContextEventIds,
                row.RoleRisk,
                Enum.Parse<Confidence>(row.Confidence),
                row.ContextCertainty,
                row.HasUnverifiedContext,
                row.ComputedAt);
    }

    public async Task AddFantasyValueAsync(
        FantasyValue value,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        database.FantasyValues.Add(FantasyValueRow.Create(
            Guid.NewGuid(),
            value.PlayerId.Value,
            value.LeagueId,
            Round(value.PerGame),
            Round(value.SeasonTotal),
            value.AdjustedProjectionId));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<FantasyValue?> GetFantasyValueAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var row = await database.FantasyValues
            .AsNoTracking()
            .Where(value =>
                value.PlayerId == playerId.Value
                && value.FantasyLeagueId == leagueId)
            .OrderByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new FantasyValue(
                new PlayerId(row.PlayerId),
                row.FantasyLeagueId,
                row.PerGame,
                row.SeasonTotal,
                row.AdjustedProjectionId);
    }

    public async Task<ProjectionDecomposition?> GetLatestDecompositionAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var baselineId = await database.BaselineProjections
            .AsNoTracking()
            .Where(value => value.PlayerId == playerId.Value)
            .OrderByDescending(value => value.ComputedAt)
            .ThenByDescending(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (baselineId is null)
        {
            return null;
        }

        var adjustedId = await database.AdjustedProjections
            .AsNoTracking()
            .Where(value => value.BaselineProjectionId == baselineId.Value)
            .OrderByDescending(value => value.ComputedAt)
            .ThenByDescending(value => value.Id)
            .Select(value => (Guid?)value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (adjustedId is null)
        {
            return null;
        }

        var observed = await GetLatestObservedAsync(playerId, cancellationToken);
        var baseline = await GetBaselineAsync(baselineId.Value, cancellationToken);
        var adjusted = await GetAdjustedAsync(adjustedId.Value, cancellationToken);
        var valueRow = await database.FantasyValues
            .AsNoTracking()
            .Where(value =>
                value.PlayerId == playerId.Value
                && value.FantasyLeagueId == leagueId
                && value.AdjustedProjectionId == adjustedId.Value)
            .OrderByDescending(value => value.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (observed is null
            || baseline is null
            || adjusted is null
            || valueRow is null)
        {
            return null;
        }

        return new ProjectionDecomposition(
            observed,
            baseline,
            adjusted,
            new FantasyValue(
                playerId,
                leagueId,
                valueRow.PerGame,
                valueRow.SeasonTotal,
                valueRow.AdjustedProjectionId));
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
