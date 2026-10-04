using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Projections;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Leagues;
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

        var observedRow = ObservedStatsRow.Create(
            observed.PlayerId.Value,
            observed.Source.SeasonEndYear,
            observed.Source.Provenance.Source,
            observed.AsOf);
        var baselineRow = BaselineProjectionRow.Create(
            baseline.Id,
            baseline.PlayerId.Value,
            Round(baseline.ProjectedMinutesPerGame),
            Serialize(baseline.PerMinuteRates),
            Serialize(baseline.ProjectedPerGame),
            baseline.ProjectedGamesPlayed,
            baseline.ComputedAt,
            baseline.ModelVersion,
            observedRow.Id);
        await SaveAndForgetAsync(cancellationToken, observedRow, baselineRow);
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
        await SaveAndForgetAsync(cancellationToken, AdjustedProjectionRow.Create(
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
        FantasyLeague league,
        DateTimeOffset computedAt,
        Guid? publicationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(league);
        if (value.LeagueId != league.Id || computedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Value must name its scoring league and a UTC timestamp.");
        }

        await SaveAndForgetAsync(cancellationToken, FantasyValueRow.Create(
            Guid.NewGuid(),
            value.PlayerId.Value,
            value.LeagueId,
            Round(value.PerGame),
            Round(value.SeasonTotal),
            value.AdjustedProjectionId,
            computedAt,
            CurrentFantasyValues.Profile(league),
            publicationId,
            value.PerGameSd is { } sd ? Round(sd) : null));
    }

    public async Task AddDistributionAsync(
        Guid baselineProjectionId,
        ProjectionDistribution distribution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        // Covariance entries can be well under 1e-4, so they keep full precision (jsonb), unlike the stat lines.
        await SaveAndForgetAsync(cancellationToken, ProjectionDistributionRow.Create(
            baselineProjectionId,
            Serialize(distribution.PerGameMean),
            JsonSerializer.Serialize(distribution.Covariance),
            distribution.Games.Alpha,
            distribution.Games.Beta,
            distribution.Games.Trials,
            distribution.ModelVersion));
    }

    /// <summary>
    /// Inserts and stops tracking: a publication writes thousands of rows in one context, and
    /// every save scans everything still tracked, so keeping them made it quadratic. Inside an
    /// import transaction the rows wait for its single save instead.
    /// </summary>
    private async Task SaveAndForgetAsync(CancellationToken cancellationToken, params object[] rows)
    {
        database.AddRange(rows);
        if (database.DeferAppends)
        {
            return;
        }

        await database.SaveChangesAsync(cancellationToken);
        foreach (var row in rows)
        {
            database.Entry(row).State = EntityState.Detached;
        }
    }

    public async Task<FantasyValue?> GetFantasyValueAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var row = (await CurrentFantasyValues.ListAsync(database, leagueId, cancellationToken))
            .SingleOrDefault(value => value.PlayerId == playerId.Value);
        return row is null
            ? null
            : new FantasyValue(
                new PlayerId(row.PlayerId),
                row.FantasyLeagueId,
                row.PerGame,
                row.SeasonTotal,
                row.AdjustedProjectionId,
                row.PerGameSd);
    }

    public async Task<ProjectionDecomposition?> GetLatestDecompositionAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var value = await GetFantasyValueAsync(playerId, leagueId, cancellationToken);
        if (value?.AdjustedProjectionId is not { } adjustedId)
        {
            return null;
        }

        var adjusted = await GetAdjustedAsync(adjustedId, cancellationToken);
        if (adjusted is null || adjusted.PlayerId != playerId)
        {
            return null;
        }

        var baselineRow = await database.BaselineProjections.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == adjusted.BaselineProjectionId
                && row.PlayerId == playerId.Value, cancellationToken);
        if (baselineRow?.ObservedStatsId is not { } observedId)
        {
            return null;
        }

        var observedRow = await database.ObservedStats.AsNoTracking()
            .SingleAsync(row => row.Id == observedId, cancellationToken);
        var source = await database.SeasonStatLines.AsNoTracking()
            .SingleAsync(row => row.PlayerId == observedRow.PlayerId
                && row.SeasonEndYear == observedRow.SeasonEndYear
                && row.Source == observedRow.Source, cancellationToken);
        var observed = new ObservedStats(playerId, Map(source), observedRow.AsOf);
        var baseline = await GetBaselineAsync(baselineRow.Id, cancellationToken);
        return new ProjectionDecomposition(observed, baseline!, adjusted, value);
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
