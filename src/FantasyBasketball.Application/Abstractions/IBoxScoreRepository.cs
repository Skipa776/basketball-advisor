using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Application.Abstractions;

public sealed record BoxScorePool(int SeasonEndYear, string Source, int GameCount,
    DateOnly LatestGameDate, DateTimeOffset LatestFetchedAt);

public interface IBoxScoreRepository
{
    Task<IReadOnlyList<BoxScorePool>> ListPoolsAsync(CancellationToken cancellationToken);

    Task<bool> AddAsync(CompletedBoxScore snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerGameSample>> ListAsync(
        int seasonEndYear, string source, NbaGamePhase phase, DateOnly throughDate, CancellationToken cancellationToken);
}
