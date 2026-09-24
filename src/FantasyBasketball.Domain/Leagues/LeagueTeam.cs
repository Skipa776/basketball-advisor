using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Leagues;

/// <summary>One fantasy team in a user's league and the NBA players on its roster.</summary>
public sealed record LeagueTeam
{
    public const int MaximumPlayers = 30;

    public LeagueTeam(Guid id, string name, bool isUsersTeam, IReadOnlyList<PlayerId> players)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(players);
        if (id == Guid.Empty || name.Trim().Length > 100)
        {
            throw new ArgumentException("A team needs an id and a name of at most 100 characters.");
        }

        if (players.Count > MaximumPlayers || players.Distinct().Count() != players.Count)
        {
            throw new ArgumentException($"A roster holds at most {MaximumPlayers} distinct players.", nameof(players));
        }

        Id = id;
        Name = name.Trim();
        IsUsersTeam = isUsersTeam;
        Players = Array.AsReadOnly(players.ToArray());
    }

    public Guid Id { get; }
    public string Name { get; }
    public bool IsUsersTeam { get; }
    public IReadOnlyList<PlayerId> Players { get; }
}
