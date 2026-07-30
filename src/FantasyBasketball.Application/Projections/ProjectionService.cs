using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Projections;

public sealed class ProjectionService(
    BaselineProjector projector,
    IProjectionRepository projections,
    ProjectionOptions options,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<BaselineProjection>> ProjectPoolAsync(
        IReadOnlyList<SeasonStatLine> pool,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pool);
        var leagueAverageRates = LeagueAverageRateCalculator.Calculate(pool, options);
        var computedAt = timeProvider.GetUtcNow();
        var results = new List<BaselineProjection>(pool.Count);

        foreach (var source in pool)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var observed = new ObservedStats(
                source.PlayerId,
                source,
                computedAt);
            var baseline = projector.Project(
                Guid.NewGuid(),
                observed,
                leagueAverageRates);
            await projections.AddAsync(observed, baseline, cancellationToken);
            results.Add(baseline);
        }

        return results;
    }
}
