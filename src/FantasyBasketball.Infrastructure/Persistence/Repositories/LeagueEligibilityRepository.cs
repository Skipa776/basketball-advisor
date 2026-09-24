using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class LeagueEligibilityRepository(FantasyDbContext database) : ILeagueEligibilityRepository
{
    public async Task<IReadOnlyDictionary<PlayerId, IReadOnlyList<string>>> ListAsync(Guid leagueId, CancellationToken cancellationToken) =>
        (await database.LeagueEligibility.AsNoTracking()
            .Where(row => row.FantasyLeagueId == leagueId)
            .ToArrayAsync(cancellationToken))
        .ToDictionary(row => new PlayerId(row.PlayerId), row => (IReadOnlyList<string>)row.Positions);

    /// <summary>Upserts the given players' eligibility; players not named keep what they had.</summary>
    public async Task SaveAsync(Guid leagueId, IReadOnlyDictionary<PlayerId, IReadOnlyList<string>> positions, CancellationToken cancellationToken)
    {
        var ids = positions.Keys.Select(id => id.Value).ToArray();
        var existing = await database.LeagueEligibility
            .Where(row => row.FantasyLeagueId == leagueId && ids.Contains(row.PlayerId))
            .ToDictionaryAsync(row => row.PlayerId, cancellationToken);
        foreach (var (player, values) in positions)
        {
            if (existing.TryGetValue(player.Value, out var row))
            {
                row.SetPositions(values.ToArray());
            }
            else
            {
                database.LeagueEligibility.Add(LeagueEligibilityRow.Create(leagueId, player.Value, values.ToArray()));
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
