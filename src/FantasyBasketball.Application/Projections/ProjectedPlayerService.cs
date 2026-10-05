using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Projections;

public sealed record ProjectedPlayer(
    int Rank,
    PlayerId PlayerId,
    string FullName,
    IReadOnlyList<string> Positions,
    decimal ProjectedSeasonValue,
    decimal? AverageDraftPosition,
    bool HasUnverifiedContext,
    bool SatOutLastSeason = false);

// The league's current published values, ranked by projected season value. Uses
// the same current-publication rule as the draft board, so the two never disagree.
public sealed class ProjectedPlayerService(
    IDraftCandidateRepository candidates,
    IPlayerRepository players)
{
    public async Task<PagedResult<ProjectedPlayer>> ListAsync(
        Guid leagueId,
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        // ponytail: loads the league's whole pool per request, like the draft board;
        // push ranking and paging into SQL if pools grow past a few thousand players.
        var ranked = (await candidates.ListAsync(leagueId, cancellationToken))
            .OrderByDescending(candidate => candidate.ProjectedSeasonValue)
            .ThenBy(candidate => candidate.PlayerId.Value)
            .ToArray();
        var offset = (int)Math.Min((long)(page - 1) * limit, ranked.Length);
        var items = new List<ProjectedPlayer>();
        foreach (var candidate in ranked.Skip(offset).Take(limit))
        {
            var player = await players.GetAsync(candidate.PlayerId, cancellationToken)
                ?? throw new ResourceNotFoundException(
                    $"Player '{candidate.PlayerId.Value}' was not found.");
            items.Add(new ProjectedPlayer(
                offset + items.Count + 1,
                candidate.PlayerId,
                player.FullName,
                candidate.Positions,
                candidate.ProjectedSeasonValue,
                candidate.AverageDraftPosition,
                candidate.HasUnverifiedContext,
                candidate.SatOutLastSeason));
        }

        return new PagedResult<ProjectedPlayer>(items, ranked.Length, page, limit);
    }
}
