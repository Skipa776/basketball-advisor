using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Application.Draft;

public sealed class DraftSessionService(
    IDraftRepository drafts,
    ILeagueRepository leagues)
{
    public async Task<DraftSession> CreateAsync(
        Guid leagueId,
        int userSlot,
        int roundCount,
        CancellationToken cancellationToken)
    {
        var league = await leagues.GetAsync(leagueId, cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"League '{leagueId}' was not found.");
        var session = new DraftSession(
            Guid.NewGuid(),
            league.TeamCount,
            roundCount,
            userSlot);
        await drafts.AddSessionAsync(session, leagueId, cancellationToken);
        return session;
    }

    public async Task<DraftPick> RecordPickAsync(
        Guid draftSessionId,
        int pickNumber,
        PlayerId playerId,
        CancellationToken cancellationToken)
    {
        var record = await RequireSessionAsync(
            draftSessionId,
            cancellationToken);
        var existing = record.Session.Picks
            .SingleOrDefault(pick => pick.PickNumber == pickNumber);
        if (existing is not null)
        {
            if (existing.PlayerId == playerId)
            {
                return existing;
            }

            throw new ResourceConflictException(
                $"Pick {pickNumber} already belongs to another player.");
        }

        if (pickNumber != record.Session.CurrentPick)
        {
            throw new ResourceConflictException(
                $"Pick {pickNumber} is not the current pick.");
        }

        DraftPick pick;
        try
        {
            pick = record.Session.MakePick(playerId);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceConflictException(exception.Message);
        }

        await drafts.AddPickAsync(pick, cancellationToken);
        return pick;
    }

    public async Task<DraftPick> UndoPickAsync(
        Guid draftSessionId,
        int pickNumber,
        CancellationToken cancellationToken)
    {
        var record = await RequireSessionAsync(
            draftSessionId,
            cancellationToken);
        if (record.Session.Picks.Count == 0
            || record.Session.Picks[^1].PickNumber != pickNumber)
        {
            throw new ResourceConflictException(
                "Only the most recent pick can be undone.");
        }

        var removed = record.Session.UndoLastPick();
        await drafts.RemoveLastPickAsync(
            draftSessionId,
            pickNumber,
            cancellationToken);
        return removed;
    }

    public async Task<DraftSessionRecord> GetAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken) =>
        await RequireSessionAsync(draftSessionId, cancellationToken);

    private async Task<DraftSessionRecord> RequireSessionAsync(
        Guid draftSessionId,
        CancellationToken cancellationToken) =>
        await drafts.GetSessionAsync(draftSessionId, cancellationToken)
            ?? throw new ResourceNotFoundException(
                $"Draft session '{draftSessionId}' was not found.");
}
