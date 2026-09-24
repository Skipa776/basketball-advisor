using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Trades;

namespace FantasyBasketball.Application.Leagues;

public sealed record TradeSideView(Guid TeamId, string Team, IReadOnlyList<string> Sends, IReadOnlyList<string> Receives);

public sealed record TradeView(string Verdict, decimal ValueBefore, decimal ValueAfter, decimal Delta, string Confidence,
    IReadOnlyList<string> StartersBefore, IReadOnlyList<string> StartersAfter, decimal ReplacementValue,
    IReadOnlyList<TradeSideView> Sides, IReadOnlyList<string> Evidence);

/// <summary>
/// R15 for points leagues: season projections from the league's published values, replacement
/// level as in the draft contract (value at rank teams × starters), your side's verdict only.
/// </summary>
public sealed class TradeService(
    ILeagueRepository leagues, ILeagueTeamRepository teams, IPlayerRepository players, IDraftCandidateRepository values)
{
    public async Task<TradeView> EvaluateAsync(Guid leagueId, IReadOnlyList<TradeLeg> legs, CancellationToken token)
    {
        var league = await leagues.GetAsync(leagueId, token) ?? throw new ResourceNotFoundException("League was not found.");
        if (league.Type != LeagueType.Points)
        {
            throw new ResourceConflictException("The trade analyzer uses points scoring; category leagues are not supported yet.");
        }

        var rosters = await teams.ListAsync(leagueId, token);
        var mine = rosters.SingleOrDefault(team => team.IsUsersTeam)
            ?? throw new ResourceConflictException("Import your league's rosters and mark your team (Mine = yes) first.");
        var projections = (await values.ListAsync(leagueId, token)).ToDictionary(value => value.PlayerId, value => value.ProjectedSeasonValue);
        var starters = league.RosterSlots.Count(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR);
        var ranked = projections.Values.OrderByDescending(value => value).ToArray();
        var replacement = ranked.Length == 0 ? 0m : ranked[Math.Min(league.TeamCount * starters, ranked.Length) - 1];

        var byTeam = new Dictionary<Guid, IReadOnlyList<TradePlayer>>();
        var names = new Dictionary<PlayerId, string>();
        foreach (var team in rosters)
        {
            var roster = new List<TradePlayer>();
            foreach (var id in team.Players)
            {
                // Trades use the primary position (owner: league eligibility is draft-only for now).
                var player = await players.GetAsync(id, token);
                var name = player?.FullName ?? "Unknown player";
                names[id] = name;
                roster.Add(new TradePlayer(id, name, player?.Positions ?? [], projections.TryGetValue(id, out var value) ? value : null));
            }

            byTeam[team.Id] = roster;
        }

        var result = TradeEvaluator.Evaluate(mine.Id, byTeam, legs, league.RosterSlots, replacement);
        var teamNames = rosters.ToDictionary(team => team.Id, team => team.Name);
        var sides = legs.SelectMany(leg => new[] { leg.FromTeamId, leg.ToTeamId }).Distinct()
            .Select(teamId => new TradeSideView(teamId, teamNames[teamId],
                legs.Where(leg => leg.FromTeamId == teamId).Select(leg => names[leg.PlayerId]).ToArray(),
                legs.Where(leg => leg.ToTeamId == teamId).Select(leg => names[leg.PlayerId]).ToArray()))
            .OrderByDescending(side => side.TeamId == mine.Id)
            .ToArray();
        return new TradeView(result.Verdict.ToString(), Math.Round(result.ValueBefore, 1), Math.Round(result.ValueAfter, 1),
            Math.Round(result.Delta, 1), result.Confidence,
            result.Before.Starters.Select(id => names[id]).ToArray(), result.After.Starters.Select(id => names[id]).ToArray(),
            Math.Round(replacement, 1), sides,
            projections.Count == 0 ? [.. result.Evidence, "No projections are published for this league yet; calculate them in Mock draft."] : result.Evidence);
    }
}
