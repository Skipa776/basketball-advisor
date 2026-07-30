using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Health;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Application.Draft;

public sealed class DraftBoardService(
    IDraftRepository drafts,
    ILeagueRepository leagues,
    IDraftCandidateRepository candidates,
    DraftBoard board,
    DraftRecommendationEngine recommendationEngine,
    DataSourceHealthService health,
    IRecommendationRepository recommendationRepository)
{
    public async Task<DraftBoardResult> GetBoardAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken)
    {
        var state = await drafts.GetSessionAsync(
            draftSessionId,
            cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"Draft session '{draftSessionId}' was not found.");
        var league = await leagues.GetAsync(state.LeagueId, cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"League '{state.LeagueId}' was not found.");
        var pool = await candidates.ListAsync(
            state.LeagueId,
            cancellationToken);
        var userRoster = state.Session.Picks
            .Where(pick => state.Session.IsUserPick(pick.PickNumber))
            .Select(pick => pick.PlayerId)
            .ToArray();
        return board.Rank(state.Session, league, pool, userRoster);
    }

    public async Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken)
    {
        var ranked = await GetBoardAsync(draftSessionId, cancellationToken);
        var sources = await health.GetAsync(cancellationToken);
        var automated = sources.Where(value =>
            value.Source == DataSourceName.BallDontLie
            || value.Source == DataSourceName.BasketballReference
            || value.Source == DataSourceName.FantasyPros).ToArray();
        var freshness = automated.Length == 0
            ? 0.5m
            : automated.Average(value => value.IsStale ? 0.25m : 1m);
        var recommendations = recommendationEngine.Recommend(
            ranked,
            new ConfidenceFactors(
                SampleSize: 0.5m,
                RoleStability: 0.5m,
                DataFreshness: freshness,
                SourceQuality: 0.8m,
                ContextCertainty: 0.5m));
        IReadOnlyList<Recommendation> final = recommendations;
        if (automated.Any(value => value.IsDegraded))
        {
            var quality = new RecommendationEvidence(
                EvidenceKind.DataQuality,
                EvidencePolarity.Risk,
                "One or more contributing data sources are stale or failed",
                automated.Count(value => value.IsDegraded));
            final = recommendations.Select(recommendation => new Recommendation(
                recommendation.Id,
                recommendation.Action,
                recommendation.SubjectPlayerId,
                recommendation.Score,
                recommendation.Confidence,
                recommendation.Evidence.Append(quality).ToArray()))
                .ToArray();
        }

        await recommendationRepository.AddRangeAsync(final, cancellationToken);
        return final;
    }
}
