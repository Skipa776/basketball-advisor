using System.Text.Json;
using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Statistics;
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
        // Set queries, not per-player lookups: the board is read on every pick and poll.
        var playerIds = values.Select(value => value.PlayerId).ToArray();
        var players = await database.Players.AsNoTracking()
            .Where(row => playerIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var adjustedIds = values.Where(value => value.AdjustedProjectionId != null)
            .Select(value => value.AdjustedProjectionId!.Value).ToArray();
        var adjustedRows = await database.AdjustedProjections.AsNoTracking()
            .Where(row => adjustedIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, cancellationToken);
        var adpRows = (await database.AdpEntries.AsNoTracking()
                .Where(row => playerIds.Contains(row.PlayerId))
                .Select(row => new { row.PlayerId, row.FetchedAt, row.Id, row.AverageDraftPosition, row.StandardDeviation })
                .ToArrayAsync(cancellationToken))
            .GroupBy(row => row.PlayerId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.FetchedAt).ThenByDescending(row => row.Id).First());
        var baselineIds = adjustedRows.Values.Select(row => row.BaselineProjectionId).ToArray();
        // The season each projection was built from; one older than the newest sat out last season.
        var observedSeasons = await database.BaselineProjections.AsNoTracking()
            .Where(row => baselineIds.Contains(row.Id))
            .Join(database.ObservedStats.AsNoTracking(), row => row.ObservedStatsId, observed => observed.Id,
                (row, observed) => new { row.Id, observed.SeasonEndYear })
            .ToDictionaryAsync(row => row.Id, row => row.SeasonEndYear, cancellationToken);
        var newestSeason = observedSeasons.Count == 0 ? 0 : observedSeasons.Values.Max();
        var distributions = await database.ProjectionDistributions.AsNoTracking()
            .Where(row => baselineIds.Contains(row.BaselineProjectionId))
            .Select(row => new { row.BaselineProjectionId, row.SeasonGames, row.GamesAlpha, row.GamesBeta })
            .ToDictionaryAsync(row => row.BaselineProjectionId, cancellationToken);
        var results = new List<DraftCandidate>(values.Count);

        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var player = players[value.PlayerId];
            var adjusted = value.AdjustedProjectionId is { } adjustedId
                ? adjustedRows.GetValueOrDefault(adjustedId)
                : null;
            var adpRow = adpRows.GetValueOrDefault(value.PlayerId);
            var adp = adpRow is null
                ? null
                : (decimal?)adpRow.AverageDraftPosition;
            var adpStandardDeviation = adpRow?.StandardDeviation;
            var games = adjusted is null || value.PerGameSd is null
                ? null
                : distributions.GetValueOrDefault(adjusted.BaselineProjectionId);
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
                adjusted?.HasUnverifiedContext ?? false,
                adpStandardDeviation,
                games is null
                    ? null
                    : new SeasonValueDistribution(value.PerGame, value.PerGameSd!.Value,
                        new BetaBinomial(games.SeasonGames, games.GamesAlpha, games.GamesBeta)),
                player.CurrentTeamId is { } team ? new NbaTeamId(team) : null,
                adjusted is not null && observedSeasons.TryGetValue(adjusted.BaselineProjectionId, out var season) && season < newestSeason));
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
