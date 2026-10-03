using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Draft;

/// <summary>
/// How a simulated opponent picks. With a fitted <see cref="OpponentChoiceModel"/>, a seeded
/// draw from its Plackett–Luce probabilities over the best-ADP candidates; otherwise lowest ADP
/// after a seeded jitter, filling open starting slots first. Shared by the live mock draft and
/// the backtest, so both measure one drafter.
/// </summary>
public static class SimulatedOpponent
{
    public static DraftCandidate Choose(
        int draftSeed,
        DraftSession session,
        RosterSlot[] starters,
        Dictionary<PlayerId, IReadOnlyList<string>> positionsByPlayer,
        DraftCandidate[] available,
        OpponentChoiceModel? model = null)
    {
        // ponytail: ADP jitter plus a greedy starting-slot check and a bench cap of 3
        // same-position players, not a learned drafter model; the upgrade is fitting pick
        // behaviour from real draft logs (E11).
        // The seed comes from the draft id bytes (HashCode.Combine is randomized per process),
        // so replays stay stable across restarts.
        var random = new Random(draftSeed ^ (session.CurrentPick * 7919));
        var team = SlotOnClock(session.CurrentPick, session.TeamCount);
        var teamPicks = session.Picks
            .Where(pick => SlotOnClock(pick.PickNumber, session.TeamCount) == team)
            .ToArray();
        var filled = FilledStarters(starters, positionsByPlayer, teamPicks);
        var startingFull = filled.Count == starters.Length;
        if (model is not null && ChooseByModel(model, random, session, starters, filled, available) is { } chosen)
        {
            return chosen;
        }

        // Only the 12 lowest-ADP available players get jittered; deeper picks are never the
        // winner anyway and drawing z for the whole pool each pick is wasted work.
        var jittered = available
            .Where(candidate => candidate.AverageDraftPosition is not null)
            .Take(12)
            .Select(candidate =>
            {
                var adp = candidate.AverageDraftPosition!.Value;
                return (
                    JitteredAdp: adp + DraftBoard.AdpSigma(adp, candidate.AdpStandardDeviation) * Normal(random),
                    Candidate: candidate);
            })
            .OrderBy(entry => entry.JitteredAdp)
            .Select(entry => entry.Candidate);
        var ranked = jittered
            .Concat(available.Where(candidate => candidate.AverageDraftPosition is not null).Skip(12))
            .Concat(available.Where(candidate => candidate.AverageDraftPosition is null))
            .ToArray();

        bool Blocked(DraftCandidate candidate)
        {
            if (startingFull)
            {
                // Bench cap: once the slots are full a real drafter stops stacking one
                // position; block a candidate when 3 held players fit entirely inside
                // his positions (a 4th pure C, or a 4th PG/SG for a G/SF candidate).
                return teamPicks.Count(pick =>
                    positionsByPlayer.TryGetValue(pick.PlayerId, out var held)
                    && held.Count > 0
                    && held.All(position => candidate.Positions.Contains(position))) >= 3;
            }

            var accepting = starters
                .Select((slot, index) => (Slot: slot, Index: index))
                .Where(pair => candidate.Positions.Any(position => pair.Slot.Accepts(position)))
                .ToArray();
            return accepting.Length > 0 && accepting.All(pair => filled.Contains(pair.Index));
        }

        return ranked.FirstOrDefault(candidate => !Blocked(candidate)) ?? ranked[0];
    }

    private static DraftCandidate? ChooseByModel(
        OpponentChoiceModel model,
        Random random,
        DraftSession session,
        RosterSlot[] starters,
        HashSet<int> filled,
        DraftCandidate[] available)
    {
        var candidates = available
            .Where(candidate => candidate.AverageDraftPosition is > 0m)
            .OrderBy(candidate => candidate.AverageDraftPosition)
            .Take(model.Candidates)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var open = starters.Where((_, index) => !filled.Contains(index)).ToArray();
        var round = ((session.CurrentPick - 1) / session.TeamCount) + 1;
        var index = model.Choose(
            round,
            candidates.Select(candidate => candidate.AverageDraftPosition!.Value).ToArray(),
            candidates.Select(candidate => open.Any(slot => candidate.Positions.Any(slot.Accepts))).ToArray(),
            (decimal)random.NextDouble());
        return candidates[index];
    }

    internal static int SlotOnClock(int pickNumber, int teamCount)
    {
        var index = (pickNumber - 1) % teamCount;
        var round = ((pickNumber - 1) / teamCount) + 1;
        return round % 2 == 1 ? index + 1 : teamCount - index;
    }

    private static HashSet<int> FilledStarters(
        RosterSlot[] starters,
        Dictionary<PlayerId, IReadOnlyList<string>> positionsByPlayer,
        IEnumerable<DraftPick> teamPicks)
    {
        var filled = new HashSet<int>();
        foreach (var pick in teamPicks)
        {
            IReadOnlyList<string> positions = positionsByPlayer.TryGetValue(pick.PlayerId, out var found) ? found : [];
            for (var index = 0; index < starters.Length; index++)
            {
                if (!filled.Contains(index) && positions.Any(position => starters[index].Accepts(position)))
                {
                    filled.Add(index);
                    break;
                }
            }
        }

        return filled;
    }

    /// <summary>
    /// Standard normal by Box-Muller; <see cref="Random"/> is seeded per pick, so two runs on
    /// the same draft id and state draw the same values.
    /// </summary>
    private static decimal Normal(Random random)
    {
        var u1 = 1d - random.NextDouble();
        var u2 = random.NextDouble();
        return (decimal)(Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2));
    }
}
