using FantasyBasketball.Domain.Schedule;

namespace FantasyBasketball.Application.Abstractions;

public sealed record ScheduledGame(NbaGame Game, string HomeAbbreviation);

public interface IGameRepository
{
    /// <summary>Final games from one schedule source starting in [fromUtc, toUtc).</summary>
    Task<IReadOnlyList<ScheduledGame>> ListFinalAsync(
        string source,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken);

    Task AddAsync(NbaGame game, CancellationToken cancellationToken);

    Task<NbaGame?> GetBySourceAsync(
        string source,
        string externalId,
        CancellationToken cancellationToken);
}
