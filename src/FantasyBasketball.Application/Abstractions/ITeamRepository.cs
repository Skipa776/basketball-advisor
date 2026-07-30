using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public interface ITeamRepository
{
    Task AddAsync(
        NbaTeam team,
        DataProvenance provenance,
        CancellationToken cancellationToken);

    Task<NbaTeam?> FindByAbbreviationAsync(
        string abbreviation,
        CancellationToken cancellationToken);
}
