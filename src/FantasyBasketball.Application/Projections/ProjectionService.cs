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
    IPlayerRepository players)
{
    /// <summary>
    /// Projects the season after <paramref name="pool"/>'s. With an active projection-rates model
    /// the hierarchical models supply rates, minutes, games and the distribution from the pool's
    /// season and the two before it; otherwise, or for anything a missing model leaves out, the
    /// baseline-v1 projector does.
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
        var positions = seasonProjector is null
            ? new Dictionary<PlayerId, string?>()
            : (await players.ListAsync(pool.Select(line => line.PlayerId).ToArray(), cancellationToken))
                .ToDictionary(player => player.Id, player => player.Positions.FirstOrDefault());
        var results = new List<ProjectedBaseline>(pool.Count);

        foreach (var source in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observed = new ObservedStats(source.PlayerId, source, computedAt);
            var baseline = projector.Project(Guid.NewGuid(), observed, leagueAverageRates);
            ProjectionDistribution? distribution = null;
            if (seasonProjector is not null)
            {
                var projection = seasonProjector.Project(
                    source.PlayerId,
                    source.SeasonEndYear + 1,
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
