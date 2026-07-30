using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class LeagueRepository(FantasyDbContext database) : ILeagueRepository
{
    public async Task AddAsync(FantasyLeague league, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(league);

        var row = FantasyLeagueRow.Create(
            league.Id,
            league.Name,
            league.Type.ToString(),
            league.TeamCount,
            league.Categories.Select(value => value.ToString()).ToArray(),
            league.Cadence.ToString());
        row.ScoringRules.AddRange(league.ScoringRules.Select((rule, ordinal) =>
            ScoringRuleRow.Create(
                league.Id,
                rule.Stat.ToString(),
                rule.PointsPerUnit,
                ordinal)));
        row.RosterSlots.AddRange(league.RosterSlots.Select((slot, ordinal) =>
            RosterSlotRow.Create(league.Id, slot.Kind.ToString(), ordinal)));

        database.FantasyLeagues.Add(row);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<FantasyLeague?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await database.FantasyLeagues
            .AsNoTracking()
            .Include(value => value.ScoringRules)
            .Include(value => value.RosterSlots)
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

        return row is null
            ? null
            : new FantasyLeague(
                row.Id,
                row.Name,
                Enum.Parse<LeagueType>(row.Type),
                row.TeamCount,
                row.ScoringRules
                    .OrderBy(rule => rule.Ordinal)
                    .Select(rule => new ScoringRule(
                        Enum.Parse<StatKey>(rule.Stat),
                        rule.PointsPerUnit))
                    .ToArray(),
                row.Categories.Select(Enum.Parse<StatKey>).ToArray(),
                row.RosterSlots
                    .OrderBy(slot => slot.Ordinal)
                    .Select(slot => new RosterSlot(Enum.Parse<RosterSlotKind>(slot.Kind)))
                    .ToArray(),
                Enum.Parse<LineupCadence>(row.Cadence));
    }

    public async Task SaveScoringAsync(
        FantasyLeague league,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(league);
        var existing = await database.FantasyLeagues
            .Include(value => value.ScoringRules)
            .SingleOrDefaultAsync(value => value.Id == league.Id, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"League '{league.Id}' was not found.");
        database.ScoringRules.RemoveRange(existing.ScoringRules);
        existing.ScoringRules.Clear();
        existing.ScoringRules.AddRange(league.ScoringRules.Select((rule, ordinal) =>
            ScoringRuleRow.Create(
                league.Id,
                rule.Stat.ToString(),
                rule.PointsPerUnit,
                ordinal)));
        await database.SaveChangesAsync(cancellationToken);
    }
}
