using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Application.Abstractions;

public interface ILeagueRepository
{
    Task AddAsync(FantasyLeague league, CancellationToken cancellationToken);

    Task<FantasyLeague?> GetAsync(Guid id, CancellationToken cancellationToken);
}
