using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Application.Abstractions;

public interface ILeagueRepository
{
    Task AddAsync(FantasyLeague league, CancellationToken cancellationToken);

    Task<FantasyLeague?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<FantasyLeague>> ListAsync(
        CancellationToken cancellationToken);

    Task SaveScoringAsync(
        FantasyLeague league,
        CancellationToken cancellationToken);

    Task SaveSettingsAsync(
        FantasyLeague league,
        CancellationToken cancellationToken);
}
