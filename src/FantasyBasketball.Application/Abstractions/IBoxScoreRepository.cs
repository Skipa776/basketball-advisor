using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Abstractions;

public interface IBoxScoreRepository
{
    Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerGameSample>> ListAsync(
        int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken);
}
