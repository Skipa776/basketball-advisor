namespace FantasyBasketball.Domain.Draft;

/// <summary>A completed public snake draft from a fantasy platform, kept to fit the opponent pick model.</summary>
public sealed record DraftLog
{
    public DraftLog(
        string source,
        string draftId,
        int season,
        int teamCount,
        int rounds,
        string? scoringType,
        IReadOnlyDictionary<string, int> slots,
        DateTimeOffset startedAt,
        IReadOnlyList<DraftLogPick> picks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(picks);
        if (teamCount is < 2 or > 32 || rounds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(teamCount), "A draft needs 2–32 teams and at least one round.");
        }

        if (picks.Select(pick => pick.PickNumber).Distinct().Count() != picks.Count)
        {
            throw new ArgumentException("Pick numbers must be unique.", nameof(picks));
        }

        Source = source;
        DraftId = draftId;
        Season = season;
        TeamCount = teamCount;
        Rounds = rounds;
        ScoringType = scoringType;
        Slots = slots;
        StartedAt = startedAt;
        Picks = picks.OrderBy(pick => pick.PickNumber).ToArray();
    }

    public string Source { get; }

    public string DraftId { get; }

    public int Season { get; }

    public int TeamCount { get; }

    public int Rounds { get; }

    public string? ScoringType { get; }

    /// <summary>Roster slots by platform name (e.g. PG, G, UTIL, BN) and count.</summary>
    public IReadOnlyDictionary<string, int> Slots { get; }

    public DateTimeOffset StartedAt { get; }

    public IReadOnlyList<DraftLogPick> Picks { get; }
}

/// <summary>One pick, with the platform's own player id, name and listed positions.</summary>
public sealed record DraftLogPick(
    int PickNumber,
    int Round,
    int DraftSlot,
    string PlayerId,
    string PlayerName,
    IReadOnlyList<string> Positions,
    string? PickedBy);
