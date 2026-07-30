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
        int userSlot,
        IReadOnlyList<DraftPick>? existingPicks = null)
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
        if (existingPicks is not null)
        {
            ValidateExistingPicks(existingPicks);
            picks.AddRange(existingPicks);
        }
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

    public bool IsUserPick(int pickNumber)
    {
        if (pickNumber < 1 || pickNumber > TeamCount * RoundCount)
        {
            return false;
        }

        var round = ((pickNumber - 1) / TeamCount) + 1;
        var slot = ((pickNumber - 1) % TeamCount) + 1;
        return round % 2 == 1
            ? slot == UserSlot
            : slot == TeamCount - UserSlot + 1;
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

    private void ValidateExistingPicks(IReadOnlyList<DraftPick> existingPicks)
    {
        var ordered = existingPicks.OrderBy(pick => pick.PickNumber).ToArray();
        if (ordered.Any(pick => pick.Id == Guid.Empty
            || pick.DraftSessionId != Id
            || pick.PickNumber < 1)
            || ordered.Select(pick => pick.PlayerId).Distinct().Count() != ordered.Length
            || ordered.Select(pick => pick.PickNumber)
                .SequenceEqual(Enumerable.Range(1, ordered.Length)) is false)
        {
            throw new ArgumentException(
                "Existing picks must be valid, unique, and contiguous.",
                nameof(existingPicks));
        }
    }
}
