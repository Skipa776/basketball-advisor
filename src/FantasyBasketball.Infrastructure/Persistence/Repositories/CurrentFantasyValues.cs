using System.Globalization;
using System.Text.Json;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

internal static class CurrentFantasyValues
{
    // Normalize decimal scale and rule order; equivalent saved profiles compare equally.
    internal static string Profile(FantasyLeague league) => JsonSerializer.Serialize(new
    {
        Type = league.Type.ToString(),
        Rules = league.ScoringRules.OrderBy(rule => rule.Stat).Select(rule => new
        {
            Stat = rule.Stat.ToString(),
            Points = decimal.Round(rule.PointsPerUnit, 4, MidpointRounding.AwayFromZero)
                .ToString("0.####", CultureInfo.InvariantCulture),
        }),
        Categories = league.Categories.Order().Select(stat => stat.ToString()),
    });

    internal static async Task<IReadOnlyList<FantasyValueRow>> ListAsync(
        FantasyDbContext database,
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var league = await new LeagueRepository(database).GetAsync(leagueId, cancellationToken);
        if (league is null)
        {
            return [];
        }

        var profile = Profile(league);
        var query = database.FantasyValues.AsNoTracking()
            .Where(value => value.FantasyLeagueId == leagueId && value.ComputedAt != null);
        var publication = await query.Where(value => value.PublicationId != null)
            .OrderByDescending(value => value.ComputedAt).ThenByDescending(value => value.Id)
            .Select(value => value.PublicationId).FirstOrDefaultAsync(cancellationToken);
        var rows = await query.Where(value => publication == null || value.PublicationId == publication)
            .GroupBy(value => value.PlayerId)
            .Select(group => group.OrderByDescending(value => value.ComputedAt)
                .ThenByDescending(value => value.Id).First())
            .ToArrayAsync(cancellationToken);
        return rows.Where(value => value.ScoringProfile == profile).ToArray();
    }
}
