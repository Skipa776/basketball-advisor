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
    IFantasyLeagueProvider leagueProvider,
    IModelVersionRepository models)
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

        var choiceModel = await models.GetActiveAsync(OpponentChoiceParameters.ModelName, token) is { } fitted
            ? new OpponentChoiceModel(OpponentChoiceParameters.Parse(fitted.ParametersJson))
            : null;
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

            var next = SimulatedOpponent.Choose(
                BitConverter.ToInt32(draftSessionId.ToByteArray()), session, starters, positionsByPlayer, available, choiceModel);
            drafted.Add(next.PlayerId);
            await drafts.AddPickAsync(session.MakePick(next.PlayerId), token);
            made++;
        }

        return made;
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
