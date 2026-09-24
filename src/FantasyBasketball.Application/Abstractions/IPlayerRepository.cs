using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Abstractions;

public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken cancellationToken);

    Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken);

    /// <summary>Records a player's current NBA team from the player directory (trades, signings).</summary>
    Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken);

    Task<Player?> FindByExternalIdentityAsync(
        string provider,
        string externalId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Player>> FindByNormalizedNameAsync(
        string normalizedName,
        CancellationToken cancellationToken);

    Task<ExternalPlayerIdentity?> FindIdentityAsync(
        PlayerId playerId,
        string provider,
        CancellationToken cancellationToken);

    Task AddResolvedIdentityAsync(
        Player player,
        ExternalPlayerIdentity identity,
        bool addPlayer,
        CancellationToken cancellationToken);

    Task AddPendingIdentityMatchAsync(
        PendingIdentityMatch pendingMatch,
        CancellationToken cancellationToken);
}
