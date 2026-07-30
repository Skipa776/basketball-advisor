using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Projections;

public sealed record ProjectionDecomposition(
    Domain.Projections.ObservedStats Observed,
    Domain.Projections.BaselineProjection Baseline,
    Domain.Projections.AdjustedProjection Adjusted,
    Domain.Projections.FantasyValue Value);

public sealed class ProjectionDecompositionService(
    IProjectionQueryRepository projections)
{
    public async Task<ProjectionDecomposition> GetAsync(
        PlayerId playerId,
        Guid leagueId,
        CancellationToken cancellationToken) =>
        await projections.GetLatestDecompositionAsync(
            playerId,
            leagueId,
            cancellationToken)
            ?? throw new ResourceNotFoundException(
                "A complete projection decomposition was not found.");
}
