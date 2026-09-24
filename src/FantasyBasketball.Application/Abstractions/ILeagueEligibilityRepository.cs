using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Abstractions;

/// <summary>
/// League-specific lineup eligibility (platforms differ: a player can be PG/SG in one league and
/// PG in another). Overrides the player's primary position; used by the draft only for now.
/// </summary>
public interface ILeagueEligibilityRepository
{
    Task<IReadOnlyDictionary<PlayerId, IReadOnlyList<string>>> ListAsync(Guid leagueId, CancellationToken cancellationToken);

    Task SaveAsync(Guid leagueId, IReadOnlyDictionary<PlayerId, IReadOnlyList<string>> positions, CancellationToken cancellationToken);
}
