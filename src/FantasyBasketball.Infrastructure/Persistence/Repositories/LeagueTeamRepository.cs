using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

/// <summary>Owned rosters; the tenancy query filter scopes every read and delete to the signed-in user.</summary>
public sealed class LeagueTeamRepository(FantasyDbContext database) : ILeagueTeamRepository
{
    public async Task<IReadOnlyList<LeagueTeam>> ListAsync(Guid leagueId, CancellationToken cancellationToken)
    {
        var rows = await database.LeagueTeams.AsNoTracking()
            .Include(team => team.Entries)
            .Where(team => team.FantasyLeagueId == leagueId)
            .OrderBy(team => team.Ordinal)
            .ToArrayAsync(cancellationToken);
        return rows.Select(team => new LeagueTeam(team.Id, team.Name, team.IsUsersTeam,
                team.Entries.OrderBy(entry => entry.Ordinal).Select(entry => new PlayerId(entry.PlayerId)).ToArray()))
            .ToArray();
    }

    public async Task ReplaceAsync(Guid leagueId, IReadOnlyList<LeagueTeam> teams, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(teams);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        database.LeagueTeams.RemoveRange(await database.LeagueTeams
            .Include(team => team.Entries)
            .Where(team => team.FantasyLeagueId == leagueId)
            .ToArrayAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < teams.Count; index++)
        {
            var team = teams[index];
            var row = LeagueTeamRow.Create(team.Id, leagueId, team.Name, team.IsUsersTeam, index);
            row.Entries.AddRange(team.Players.Select((player, ordinal) => LeagueRosterEntryRow.Create(team.Id, player.Value, ordinal)));
            database.LeagueTeams.Add(row);
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
