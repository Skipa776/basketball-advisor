using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Backtest;

public sealed record DraftBenchmarkResult(
    int Drafts,
    decimal MeanBoard,
    decimal MeanAdpBot,
    decimal MeanDifference,
    decimal CiLow,
    decimal CiHigh,
    IReadOnlyList<decimal> Differences);

/// <summary>
/// Seeded mock drafts in which the user drafts once by the production board and once by
/// lowest ADP, against the same simulated opponents. Each roster is scored by its best
/// starting lineup's <b>actual</b> season value, never its projection (backtest_contract).
/// </summary>
public sealed class DraftBenchmark(DraftBoard board)
{
    private const decimal Z95 = 1.96m;

    public DraftBenchmarkResult Run(
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        IReadOnlyDictionary<PlayerId, decimal> actualSeasonValue,
        int drafts = 200)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(actualSeasonValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(drafts, 2);
        var boardValues = new decimal[drafts];
        var adpValues = new decimal[drafts];
        for (var index = 0; index < drafts; index++)
        {
            var userSlot = (index % league.TeamCount) + 1;
            var seed = (index * 7919) + 17;
            boardValues[index] = LineupValue(Draft(league, candidates, userSlot, seed, BoardPick), league, actualSeasonValue);
            adpValues[index] = LineupValue(Draft(league, candidates, userSlot, seed, AdpPick), league, actualSeasonValue);
        }

        var differences = boardValues.Zip(adpValues, (boardValue, adpValue) => boardValue - adpValue).ToArray();
        var mean = differences.Average();
        var halfWidth = Z95 * StandardDeviation(differences, mean) / Sqrt(drafts);
        return new DraftBenchmarkResult(
            drafts, boardValues.Average(), adpValues.Average(), mean, mean - halfWidth, mean + halfWidth, differences);
    }

    /// <summary>Best starting lineup by actual value: highest first into the first open slot it fits.</summary>
    public static decimal LineupValue(
        IEnumerable<DraftCandidate> roster,
        FantasyLeague league,
        IReadOnlyDictionary<PlayerId, decimal> actualSeasonValue)
    {
        var open = Starters(league).ToList();
        var total = 0m;
        foreach (var player in roster.OrderByDescending(player => actualSeasonValue.GetValueOrDefault(player.PlayerId)))
        {
            var slot = open.FirstOrDefault(slot => player.Positions.Any(slot.Accepts));
            if (slot is not null)
            {
                open.Remove(slot);
                total += actualSeasonValue.GetValueOrDefault(player.PlayerId);
            }
        }

        return total;
    }

    private IReadOnlyList<DraftCandidate> Draft(
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        int userSlot,
        int seed,
        Func<DraftSession, FantasyLeague, IReadOnlyList<DraftCandidate>, List<PlayerId>, DraftCandidate> userPick)
    {
        var rounds = league.RosterSlots.Count(slot => slot.Kind is not RosterSlotKind.IR);
        var session = new DraftSession(Guid.NewGuid(), league.TeamCount, rounds, userSlot);
        var starters = Starters(league).ToArray();
        var positions = candidates.ToDictionary(candidate => candidate.PlayerId, candidate => candidate.Positions);
        var byId = candidates.ToDictionary(candidate => candidate.PlayerId);
        var userRoster = new List<PlayerId>();
        while (session.CurrentPick <= league.TeamCount * rounds)
        {
            var drafted = session.Picks.Select(pick => pick.PlayerId).ToHashSet();
            var available = candidates.Where(candidate => !drafted.Contains(candidate.PlayerId)).ToArray();
            if (available.Length == 0)
            {
                break;
            }

            var isUser = session.IsUserPick(session.CurrentPick);
            var choice = isUser
                ? userPick(session, league, candidates, userRoster)
                : SimulatedOpponent.Choose(seed, session, starters, positions, available);
            session.MakePick(choice.PlayerId);
            if (isUser)
            {
                userRoster.Add(choice.PlayerId);
            }
        }

        return userRoster.Select(id => byId[id]).ToArray();
    }

    private DraftCandidate BoardPick(
        DraftSession session,
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        List<PlayerId> userRoster)
    {
        var top = board.Rank(session, league, candidates, userRoster).Rankings[0].PlayerId;
        return candidates.First(candidate => candidate.PlayerId == top);
    }

    private static DraftCandidate AdpPick(
        DraftSession session,
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        List<PlayerId> userRoster)
    {
        var drafted = session.Picks.Select(pick => pick.PlayerId).ToHashSet();
        return candidates
            .Where(candidate => !drafted.Contains(candidate.PlayerId))
            .OrderBy(candidate => candidate.AverageDraftPosition ?? decimal.MaxValue)
            .ThenByDescending(candidate => candidate.ProjectedSeasonValue)
            .ThenBy(candidate => candidate.PlayerId.Value)
            .First();
    }

    private static IEnumerable<RosterSlot> Starters(FantasyLeague league) =>
        league.RosterSlots.Where(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR);

    private static decimal StandardDeviation(decimal[] values, decimal mean) =>
        Sqrt(values.Sum(value => (value - mean) * (value - mean)) / (values.Length - 1));

    private static decimal Sqrt(decimal value) => (decimal)Math.Sqrt((double)value);
}
