using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Abstractions;

public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken cancellationToken);

    Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken);
}
