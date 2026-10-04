using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Scoring;

namespace FantasyBasketball.Application.Projections;

public sealed record ProjectionPublication(
    int SeasonEndYear, string Source, int PlayerCount, DateTimeOffset ComputedAt);

public sealed class LeagueProjectionService(
    ILeagueRepository leagues,
    ISeasonStatLineRepository statistics,
    ProjectionService projector,
    IProjectionRepository projections,
    IContextEventRepository context,
    ContextApplier applier,
    PointsScoringEngine scoring,
    IImportTransaction transaction,
    TimeProvider clock)
{
    public Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken) =>
        statistics.ListPoolsAsync(cancellationToken);

    /// <summary>
    /// Publishes from the newest imported season (Basketball-Reference first), so a new or
    /// rescored points league is ready to draft without a manual step. Null when there is nothing
    /// to publish from: no imported season, or a category league.
    /// </summary>
    public async Task<ProjectionPublication?> PublishLatestAsync(Guid leagueId, CancellationToken cancellationToken)
    {
        var league = await leagues.GetAsync(leagueId, cancellationToken);
        var pool = (await statistics.ListPoolsAsync(cancellationToken))
            .OrderByDescending(pool => pool.SeasonEndYear)
            .ThenByDescending(pool => pool.Source == DataSourceName.BasketballReference)
            .FirstOrDefault();
        return league?.Type != LeagueType.Points || pool is null
            ? null
            : await RecalculateAsync(leagueId, pool.SeasonEndYear, pool.Source, cancellationToken);
    }

    public async Task<ProjectionPublication> RecalculateAsync(
        Guid leagueId, int seasonEndYear, string source, CancellationToken cancellationToken)
    {
        ProjectionPublication? result = null;
        await transaction.ExecuteAsync(async token =>
        {
            var league = await leagues.GetAsync(leagueId, token)
                ?? throw new ResourceNotFoundException("League was not found.");
            if (league.Type != LeagueType.Points)
            {
                throw new ResourceConflictException("Projection recalculation currently requires a points league.");
            }

            var pool = await statistics.ListPoolAsync(seasonEndYear, source, token);
            if (pool.Count == 0)
            {
                throw new ResourceConflictException("Import season statistics for this source before calculating projections.");
            }

            var computedAt = clock.GetUtcNow();
            var publicationId = Guid.NewGuid();
            var projected = await projector.ProjectPoolAsync(pool, token);
            foreach (var (baseline, distribution) in projected)
            {
                token.ThrowIfCancellationRequested();
                var events = await context.ListForPlayerAsync(baseline.PlayerId, token);
                var adjusted = applier.Apply(Guid.NewGuid(), baseline,
                    events.Select(item => item.Event).ToArray(),
                    events.Select(item => item.Impact).ToArray(), computedAt);
                await projections.AddAdjustedAsync(adjusted, token);
                var perGame = scoring.Score(adjusted.ProjectedPerGame, league);
                // ponytail: the SD comes from the unadjusted distribution; context shifts the mean only.
                var perGameSd = distribution?.FantasyPointsPerGame(league.ScoringRules).Sd;
                await projections.AddFantasyValueAsync(new FantasyValue(
                    baseline.PlayerId, leagueId, perGame,
                    perGame * baseline.ProjectedGamesPlayed, adjusted.Id, perGameSd), league, computedAt, publicationId, token);
            }

            result = new ProjectionPublication(seasonEndYear, source, pool.Count, computedAt);
        }, cancellationToken);
        return result ?? throw new InvalidOperationException("Projection publication did not complete.");
    }
}
