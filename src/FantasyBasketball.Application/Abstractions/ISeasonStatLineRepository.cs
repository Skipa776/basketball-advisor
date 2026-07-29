using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Abstractions;

public interface ISeasonStatLineRepository
{
    Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken);

    Task<SeasonStatLine?> GetAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        CancellationToken cancellationToken);
}
