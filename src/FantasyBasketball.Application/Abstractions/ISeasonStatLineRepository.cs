using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Application.Abstractions;

public sealed record SeasonProjectionPool(int SeasonEndYear, string Source, int PlayerCount);

public interface ISeasonStatLineRepository
{
    Task<IReadOnlyList<SeasonProjectionPool>> ListPoolsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SeasonStatLine>> ListPoolAsync(
        int seasonEndYear, string source, CancellationToken cancellationToken);

    Task AddAsync(SeasonStatLine statLine, CancellationToken cancellationToken);

    Task<SeasonStatLine?> GetAsync(
        PlayerId playerId,
        int seasonEndYear,
        string source,
        CancellationToken cancellationToken);
}
