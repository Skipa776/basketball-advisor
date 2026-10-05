using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Abstractions;

public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken cancellationToken);

    Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken);

    /// <summary>Several players in one read; the default loops <see cref="GetAsync"/>.</summary>
    async Task<IReadOnlyList<Player>> ListAsync(IReadOnlyCollection<PlayerId> ids, CancellationToken cancellationToken)
    {
        var players = new List<Player>(ids.Count);
        foreach (var id in ids)
        {
            if (await GetAsync(id, cancellationToken) is { } player)
            {
                players.Add(player);
            }
        }

        return players;
    }

    /// <summary>Records a player's current NBA team from the player directory (trades, signings).</summary>
    Task SaveCurrentTeamAsync(PlayerId id, NbaTeamId? teamId, CancellationToken cancellationToken);

    /// <summary>Records a player's lineup positions from the owner's chosen eligibility source.</summary>
    Task SavePositionsAsync(PlayerId id, IReadOnlyList<string> positions, CancellationToken cancellationToken);

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
