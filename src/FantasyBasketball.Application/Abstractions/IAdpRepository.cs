using FantasyBasketball.Domain.Players;
using CanonicalAdpEntry = FantasyBasketball.Domain.Draft.AdpEntry;

namespace FantasyBasketball.Application.Abstractions;

public interface IAdpRepository
{
    Task AddAsync(
        CanonicalAdpEntry entry,
        CancellationToken cancellationToken);

    Task<CanonicalAdpEntry?> GetLatestAsync(
        PlayerId playerId,
        CancellationToken cancellationToken);
}
