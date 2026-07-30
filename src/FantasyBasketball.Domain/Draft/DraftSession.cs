using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Draft;

public sealed record DraftPick(
    Guid Id,
    Guid DraftSessionId,
    PlayerId PlayerId,
    int PickNumber);

public sealed class DraftSession
{
    private readonly List<DraftPick> picks = [];

    public DraftSession(
        Guid id,
        int teamCount,
        int roundCount,
        int userSlot)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Draft session id cannot be empty.", nameof(id));
        }

        if (teamCount <= 0 || roundCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(teamCount));
        }

        if (userSlot < 1 || userSlot > teamCount)
        {
            throw new ArgumentOutOfRangeException(nameof(userSlot));
        }

        Id = id;
        TeamCount = teamCount;
        RoundCount = roundCount;
        UserSlot = userSlot;
    }

    public Guid Id { get; }

    public int TeamCount { get; }

    public int RoundCount { get; }

    public int UserSlot { get; }

    public int CurrentPick => picks.Count + 1;

    public IReadOnlyList<DraftPick> Picks =>
        new ReadOnlyCollection<DraftPick>(picks.ToArray());

    public int PicksUntilNextTurn
    {
        get
        {
            var next = Enumerable.Range(1, RoundCount)
                .Select(round => round % 2 == 1
                    ? ((round - 1) * TeamCount) + UserSlot
                    : (round * TeamCount) - UserSlot + 1)
                .FirstOrDefault(pick => pick > CurrentPick);
            return next == 0 ? 1 : Math.Max(1, next - CurrentPick);
        }
    }

    public DraftPick MakePick(PlayerId playerId)
    {
        if (picks.Any(pick => pick.PlayerId == playerId))
        {
            throw new InvalidOperationException("Player has already been drafted.");
        }

        var pick = new DraftPick(
            Guid.NewGuid(),
            Id,
            playerId,
            CurrentPick);
        picks.Add(pick);
        return pick;
    }

    public DraftPick UndoLastPick()
    {
        if (picks.Count == 0)
        {
            throw new InvalidOperationException("There is no pick to undo.");
        }

        var pick = picks[^1];
        picks.RemoveAt(picks.Count - 1);
        return pick;
    }
}
