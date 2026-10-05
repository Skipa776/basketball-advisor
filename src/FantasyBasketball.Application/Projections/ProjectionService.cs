using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Projections;

/// <summary>A published baseline and, when every M2 model is active, its distribution.</summary>
public sealed record ProjectedBaseline(BaselineProjection Baseline, ProjectionDistribution? Distribution);

public sealed class ProjectionService(
    BaselineProjector projector,
    IProjectionRepository projections,
    ProjectionOptions options,
    TimeProvider timeProvider,
    IModelVersionRepository models,
    ISeasonStatLineRepository statistics,
    IPlayerRepository players,
    IAdpRepository? adp = null)
{
    /// <summary>
    /// Projects the season after <paramref name="pool"/>'s. With an active projection-rates model
    /// the hierarchical models supply rates, minutes, games and the distribution from the pool's
    /// season and the two before it; otherwise, or for anything a missing model leaves out, the
    /// baseline-v1 projector does. Players who sat out the pool's whole season are projected too
    /// (from the two seasons before it) when drafters expect them back: they have ADP published
    /// after that season ended.
    /// </summary>
    public async Task<IReadOnlyList<ProjectedBaseline>> ProjectPoolAsync(
        IReadOnlyList<SeasonStatLine> pool,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);
        var leagueAverageRates = LeagueAverageRateCalculator.Calculate(pool, options);
        var computedAt = timeProvider.GetUtcNow();
        var seasonProjector = await SeasonProjectorAsync(cancellationToken);
        var history = seasonProjector is null || pool.Count == 0 ? [] : await HistoryAsync(pool, cancellationToken);
        var byPlayer = history.ToLookup(line => line.PlayerId);
        var seasonGames = SeasonProjector.SeasonLengths(history);
        var targetSeason = pool.Count == 0 ? 0 : pool[0].SeasonEndYear + 1;
        var sources = pool.Concat(await ReturningAsync(pool, history, cancellationToken)).ToArray();
        var positions = seasonProjector is null
            ? new Dictionary<PlayerId, string?>()
            : (await players.ListAsync(sources.Select(line => line.PlayerId).ToArray(), cancellationToken))
                .ToDictionary(player => player.Id, player => player.Positions.FirstOrDefault());
        var results = new List<ProjectedBaseline>(sources.Length);

        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observed = new ObservedStats(source.PlayerId, source, computedAt);
            var baseline = projector.Project(Guid.NewGuid(), observed, leagueAverageRates);
            ProjectionDistribution? distribution = null;
            if (seasonProjector is not null)
            {
                var projection = seasonProjector.Project(
                    source.PlayerId,
                    targetSeason,
                    positions.GetValueOrDefault(source.PlayerId),
                    byPlayer[source.PlayerId].ToArray(),
                    seasonGames,
                    baseline.ProjectedMinutesPerGame);
                distribution = projection.Distribution;
                baseline = new BaselineProjection(
                    baseline.Id,
                    baseline.PlayerId,
                    projection.Minutes,
                    projection.PerMinute,
                    projection.PerGame,
                    distribution is null
                        ? baseline.ProjectedGamesPlayed
                        : decimal.ToInt32(decimal.Round(distribution.Games.Mean, 0, MidpointRounding.AwayFromZero)),
                    computedAt,
                    seasonProjector.ModelVersion);
            }

            await projections.AddAsync(observed, baseline, cancellationToken);
            if (distribution is not null)
            {
                await projections.AddDistributionAsync(baseline.Id, distribution, cancellationToken);
            }

            results.Add(new ProjectedBaseline(baseline, distribution));
        }

        return results;
    }

    private async Task<SeasonProjector?> SeasonProjectorAsync(CancellationToken cancellationToken)
    {
        if (await models.GetActiveAsync(ProjectionRateParameters.ModelName, cancellationToken) is not { } rates)
        {
            return null;
        }

        var minutes = await models.GetActiveAsync(MinutesModelParameters.ModelName, cancellationToken);
        var availability = await models.GetActiveAsync(AvailabilityModelParameters.ModelName, cancellationToken);
        var covariance = await models.GetActiveAsync(StatCovarianceParameters.ModelName, cancellationToken);
        var version = string.Join("+", new[] { rates, minutes, availability, covariance }
            .Where(model => model is not null)
            .Select(model => model!.Version));
        return new SeasonProjector(
            ProjectionRateParameters.Parse(rates.ParametersJson),
            minutes is null ? null : MinutesModelParameters.Parse(minutes.ParametersJson),
            availability is null ? null : AvailabilityModelParameters.Parse(availability.ParametersJson),
            covariance is null ? null : StatCovarianceParameters.Parse(covariance.ParametersJson),
            version);
    }

    /// <summary>
    /// The latest line of each player missing from the pool's season but in one of the two before
    /// it, kept only with ADP fetched after the pool's season ended (so not retired or released).
    /// </summary>
    private async Task<IReadOnlyList<SeasonStatLine>> ReturningAsync(
        IReadOnlyList<SeasonStatLine> pool, IReadOnlyList<SeasonStatLine> history, CancellationToken cancellationToken)
    {
        if (adp is null || pool.Count == 0 || history.Count == 0)
        {
            return [];
        }

        var seasonEnded = new DateTimeOffset(pool[0].SeasonEndYear, SeasonEndMonth, 1, 0, 0, 0, TimeSpan.Zero);
        var inPool = pool.Select(line => line.PlayerId).ToHashSet();
        var returning = new List<SeasonStatLine>();
        foreach (var latest in history.Where(line => !inPool.Contains(line.PlayerId))
            .GroupBy(line => line.PlayerId)
            .Select(group => group.MaxBy(line => line.SeasonEndYear)!))
        {
            if (await adp.GetLatestAsync(latest.PlayerId, cancellationToken) is { } entry && entry.Provenance.FetchedAt >= seasonEnded)
            {
                returning.Add(latest);
            }
        }

        return returning;
    }

    /// <summary>A regular season is over by July; ADP fetched from then on is for the next one.</summary>
    private const int SeasonEndMonth = 7;

    /// <summary>The pool's season and the two before it, from the pool's source.</summary>
    private async Task<IReadOnlyList<SeasonStatLine>> HistoryAsync(IReadOnlyList<SeasonStatLine> pool, CancellationToken cancellationToken)
    {
        var season = pool[0].SeasonEndYear;
        var source = pool.Select(line => line.Provenance.Source).GroupBy(name => name).MaxBy(group => group.Count())!.Key;
        var history = new List<SeasonStatLine>(pool);
        for (var lag = 1; lag <= 2; lag++)
        {
            history.AddRange(await statistics.ListPoolAsync(season - lag, source, cancellationToken));
        }

        return history;
    }
}
