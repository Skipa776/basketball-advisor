using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Application.Abstractions;

public interface ILeagueTeamRepository
{
    Task<IReadOnlyList<LeagueTeam>> ListAsync(Guid leagueId, CancellationToken cancellationToken);

    /// <summary>Rosters are current state: an import replaces the league's teams in one transaction.</summary>
    Task ReplaceAsync(Guid leagueId, IReadOnlyList<LeagueTeam> teams, CancellationToken cancellationToken);
}
