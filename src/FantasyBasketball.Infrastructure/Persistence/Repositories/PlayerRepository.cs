using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class PlayerRepository(FantasyDbContext database) : IPlayerRepository
{
    public async Task AddAsync(Player player, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(player);
        database.Players.Add(PlayerRow.Create(
            player.Id.Value,
            player.FullName,
            player.NormalizedName,
            player.Positions.ToArray(),
            player.BirthDate,
            player.CurrentTeamId?.Value));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<Player?> GetAsync(PlayerId id, CancellationToken cancellationToken)
    {
        var row = await database.Players
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id.Value, cancellationToken);

        return row is null
            ? null
            : new Player(
                new PlayerId(row.Id),
                row.FullName,
                row.NormalizedName,
                row.CurrentTeamId is { } teamId ? new NbaTeamId(teamId) : null,
                row.Positions,
                row.BirthDate);
    }
}
