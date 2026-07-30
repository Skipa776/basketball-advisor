using FantasyBasketball.Domain.Schedule;

namespace FantasyBasketball.Application.Abstractions;

public interface IGameRepository
{
    Task AddAsync(NbaGame game, CancellationToken cancellationToken);

    Task<NbaGame?> GetBySourceAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken);
}
