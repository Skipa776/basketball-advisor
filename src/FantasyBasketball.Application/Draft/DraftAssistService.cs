using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Draft;

public sealed record TakenPicksResult(int Recorded, int CurrentPick, string? StoppedAt, string? Reason);

/// <summary>
/// The three ways other teams' picks get into a draft: simulated opponents for a solo mock,
/// names pasted from the real draft room, or the league's Sleeper draft room synced directly.
/// </summary>
public sealed class DraftAssistService(
    IDraftRepository drafts,
    ILeagueRepository leagues,
    IDraftCandidateRepository candidates,
    IPlayerRepository players,
    IFantasyLeagueProvider leagueProvider)
{
    /// <summary>
    /// Other teams pick until it is the user's turn: each one takes the lowest ADP among the
    /// available players after jittering it by the same sigma the recommender uses, skipping
    /// players whose team's starting slots are already filled; players with no ADP go last by
    /// projected value. Deterministic for a draft id and state, so a replayed mock plays out
    /// the same way.
    /// </summary>
    public async Task<int> SimulateToUserTurnAsync(Guid draftSessionId, CancellationToken token)
    {
        var record = await RequireAsync(draftSessionId, token);
        var session = record.Session;
        var ordered = (await candidates.ListAsync(record.LeagueId, token))
            .OrderBy(candidate => candidate.AverageDraftPosition ?? decimal.MaxValue)
            .ThenByDescending(candidate => candidate.ProjectedSeasonValue)
            .ThenBy(candidate => candidate.PlayerId.Value)
            .ToArray();
        if (ordered.Length == 0)
        {
            throw new ResourceConflictException("Calculate projections for this league before simulating other teams.");
        }

        var league = await leagues.GetAsync(record.LeagueId, token)
            ?? throw new ResourceNotFoundException($"League '{record.LeagueId}' was not found.");
        var starters = league.RosterSlots
            .Where(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR)
            .ToArray();
        var positionsByPlayer = ordered.ToDictionary(
            candidate => candidate.PlayerId,
            candidate => candidate.Positions);

        var drafted = session.Picks.Select(pick => pick.PlayerId).ToHashSet();
        var made = 0;
        while (!IsComplete(session) && !session.IsUserPick(session.CurrentPick))
        {
            var available = ordered
                .Where(candidate => !drafted.Contains(candidate.PlayerId))
                .ToArray();
            if (available.Length == 0)
            {
                break;
            }

            var next = ChooseSimulatedPick(draftSessionId, session, starters, positionsByPlayer, available);
            drafted.Add(next.PlayerId);
            await drafts.AddPickAsync(session.MakePick(next.PlayerId), token);
            made++;
        }

        return made;
    }

    private static DraftCandidate ChooseSimulatedPick(
        Guid draftSessionId,
        DraftSession session,
        RosterSlot[] starters,
        Dictionary<PlayerId, IReadOnlyList<string>> positionsByPlayer,
        DraftCandidate[] available)
    {
        // ponytail: ADP jitter plus a greedy starting-slot check, not a learned drafter model;
        // the upgrade is fitting pick behaviour from real draft logs (E11).
        // HashCode.Combine is randomized per process; the draft id bytes keep replays stable across restarts.
        var random = new Random(BitConverter.ToInt32(draftSessionId.ToByteArray()) ^ (session.CurrentPick * 7919));
        var team = SlotOnClock(session.CurrentPick, session.TeamCount);
        var filled = FilledStarters(
            starters,
            positionsByPlayer,
            session.Picks.Where(pick => SlotOnClock(pick.PickNumber, session.TeamCount) == team));
        var startingFull = filled.Count == starters.Length;

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
                return false;
            }

            var accepting = starters
                .Select((slot, index) => (Slot: slot, Index: index))
                .Where(pair => candidate.Positions.Any(position => pair.Slot.Accepts(position)))
                .ToArray();
            return accepting.Length > 0 && accepting.All(pair => filled.Contains(pair.Index));
        }

        return ranked.FirstOrDefault(candidate => !Blocked(candidate)) ?? ranked[0];
    }

    private static int SlotOnClock(int pickNumber, int teamCount)
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

    /// <summary>
    /// Records pasted names in order at the current pick, whoever's turn it is. Stops at the first
    /// name that is unknown, ambiguous or already drafted, so nothing is guessed.
    /// </summary>
    public async Task<TakenPicksResult> RecordTakenAsync(Guid draftSessionId, IReadOnlyList<string> names, CancellationToken token)
    {
        var session = (await RequireAsync(draftSessionId, token)).Session;
        var recorded = 0;
        foreach (var name in names.Select(value => value.Trim()).Where(value => value.Length > 0))
        {
            if (IsComplete(session))
            {
                return new TakenPicksResult(recorded, session.CurrentPick, name, "The draft is complete.");
            }

            var matches = await players.FindByNormalizedNameAsync(PlayerName.Normalize(name), token);
            if (matches.Count != 1)
            {
                return new TakenPicksResult(recorded, session.CurrentPick, name,
                    matches.Count == 0 ? "No player has that name." : "More than one player has that name; pick him from the list.");
            }

            if (session.Picks.Any(pick => pick.PlayerId == matches[0].Id))
            {
                return new TakenPicksResult(recorded, session.CurrentPick, name, "Already drafted.");
            }

            await drafts.AddPickAsync(session.MakePick(matches[0].Id), token);
            recorded++;
        }

        return new TakenPicksResult(recorded, session.CurrentPick, null, null);
    }

    /// <summary>
    /// Pulls the other teams' picks from the league's latest Sleeper draft room instead of pasting
    /// names. Picks from the current pick onward go through the same record path, so the same
    /// matching and stop rules apply.
    /// </summary>
    public async Task<TakenPicksResult> SyncFromSleeperAsync(Guid draftSessionId, string sleeperLeagueId, CancellationToken token)
    {
        var session = (await RequireAsync(draftSessionId, token)).Session;
        var draft = await leagueProvider.GetLatestDraftAsync(sleeperLeagueId, token);
        if (draft is null)
        {
            throw new ResourceConflictException("This Sleeper league has no draft yet.");
        }

        if (draft.TeamCount != session.TeamCount)
        {
            throw new ResourceConflictException($"That Sleeper draft has {draft.TeamCount} teams; this draft session has {session.TeamCount}.");
        }

        var names = draft.Picks
            .Where(pick => pick.PickNumber >= session.CurrentPick)
            .OrderBy(pick => pick.PickNumber)
            .Select(pick => pick.FullName)
            .ToArray();
        if (names.Length == 0)
        {
            return new TakenPicksResult(0, session.CurrentPick, null, null);
        }

        return await RecordTakenAsync(draftSessionId, names, token);
    }

    private static bool IsComplete(DraftSession session) => session.CurrentPick > session.TeamCount * session.RoundCount;

    private async Task<DraftSessionRecord> RequireAsync(Guid id, CancellationToken token) =>
        await drafts.GetSessionAsync(id, token) ?? throw new ResourceNotFoundException($"Draft session '{id}' was not found.");
}
