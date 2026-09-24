using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class DraftCandidateRepository(FantasyDbContext database)
    : IDraftCandidateRepository
{
    public async Task<IReadOnlyList<DraftCandidate>> ListAsync(
        Guid leagueId,
        CancellationToken cancellationToken)
    {
        var values = await CurrentFantasyValues.ListAsync(database, leagueId, cancellationToken);
        // League-specific eligibility (e.g. PG/SG on this platform) wins over the primary position.
        var eligibility = await database.LeagueEligibility.AsNoTracking()
            .Where(row => row.FantasyLeagueId == leagueId)
            .ToDictionaryAsync(row => row.PlayerId, row => row.Positions, cancellationToken);
        var results = new List<DraftCandidate>(values.Count);

        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var player = await database.Players
                .AsNoTracking()
                .SingleAsync(row => row.Id == value.PlayerId, cancellationToken);
            var adjusted = value.AdjustedProjectionId is { } adjustedId
                ? await database.AdjustedProjections
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        row => row.Id == adjustedId,
                        cancellationToken)
                : null;
            var adp = await database.AdpEntries
                .AsNoTracking()
                .Where(row => row.PlayerId == value.PlayerId)
                .OrderByDescending(row => row.FetchedAt)
                .ThenByDescending(row => row.Id)
                .Select(row => (decimal?)row.AverageDraftPosition)
                .FirstOrDefaultAsync(cancellationToken);
            var categoryTotals = adjusted is null
                ? new Dictionary<StatKey, decimal>()
                : Deserialize(adjusted.ProjectedPerGame).Values.ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value);

            results.Add(new DraftCandidate(
                new PlayerId(value.PlayerId),
                value.SeasonTotal,
                eligibility.GetValueOrDefault(value.PlayerId) ?? player.Positions,
                adp,
                0m,
                0m,
                adjusted?.RoleRisk ?? 0m,
                categoryTotals,
                adjusted?.HasUnverifiedContext ?? false));
        }

        return results;
    }

    private static StatLine Deserialize(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)
            ?? throw new InvalidOperationException(
                "Persisted StatLine JSON was null.");
        return new StatLine(values.ToDictionary(
            entry => Enum.Parse<StatKey>(entry.Key),
            entry => entry.Value));
    }
}
