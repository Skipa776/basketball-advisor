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

/// <summary>How a benchmarked user drafter picks, given the draft so far.</summary>
public delegate DraftCandidate UserPicker(
    DraftSession session, FantasyLeague league, IReadOnlyList<DraftCandidate> candidates, List<PlayerId> userRoster);

/// <summary>One drafter against another over the same seeded drafts: mean paired difference and 95% CI.</summary>
public sealed record DrafterComparison(string Drafter, string Against, decimal MeanDifference, decimal CiLow, decimal CiHigh);

public sealed record DraftComparisonResult(int Drafts, IReadOnlyDictionary<string, decimal> MeanValue, IReadOnlyList<DrafterComparison> Comparisons);

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

    /// <summary>
    /// Every named drafter drafts the same seeded drafts against the same simulated opponents;
    /// each drafter is compared with every one listed before it, paired by draft.
    /// </summary>
    public DraftComparisonResult Compare(
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        IReadOnlyDictionary<PlayerId, decimal> actualSeasonValue,
        IReadOnlyList<(string Name, UserPicker Pick)> drafters,
        int drafts = 200)
    {
        ArgumentNullException.ThrowIfNull(drafters);
        ArgumentOutOfRangeException.ThrowIfLessThan(drafts, 2);
        var values = drafters.ToDictionary(drafter => drafter.Name, _ => new decimal[drafts]);
        for (var index = 0; index < drafts; index++)
        {
            var userSlot = (index % league.TeamCount) + 1;
            var seed = (index * 7919) + 17;
            foreach (var (name, pick) in drafters)
            {
                values[name][index] = LineupValue(Draft(league, candidates, userSlot, seed, pick), league, actualSeasonValue);
            }
        }

        var comparisons = new List<DrafterComparison>();
        for (var i = 1; i < drafters.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                var differences = values[drafters[i].Name].Zip(values[drafters[j].Name], (a, b) => a - b).ToArray();
                var mean = differences.Average();
                var halfWidth = Z95 * StandardDeviation(differences, mean) / Sqrt(drafts);
                comparisons.Add(new DrafterComparison(drafters[i].Name, drafters[j].Name, mean, mean - halfWidth, mean + halfWidth));
            }
        }

        return new DraftComparisonResult(drafts, values.ToDictionary(pair => pair.Key, pair => pair.Value.Average()), comparisons);
    }

    /// <summary>The production heuristic board's top-ranked player.</summary>
    public UserPicker BoardDrafter => BoardPick;

    /// <summary>Lowest ADP left: the room's consensus.</summary>
    public static UserPicker AdpDrafter => AdpPick;

    /// <summary>The draft simulator's top pick (draft_simulation_contract), seeded by draft and pick.</summary>
    public static UserPicker SimulatorDrafter(DraftSimulator simulator, LineupOptimizer lineup) =>
        (session, _, candidates, _) =>
        {
            var seed = BitConverter.ToInt32(session.Id.ToByteArray()) ^ (session.CurrentPick * 7919);
            var top = simulator.Simulate(session, lineup, candidates, seed, RiskMode.Mean).Candidates[0].PlayerId;
            return candidates.First(candidate => candidate.PlayerId == top);
        };

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
        UserPicker userPick)
    {
        var rounds = league.RosterSlots.Count(slot => slot.Kind is not RosterSlotKind.IR);
        // The session id follows the seed so every drafter, simulator included, replays identically.
        var session = new DraftSession(new Guid(seed, 0, 0, new byte[8]), league.TeamCount, rounds, userSlot);
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
