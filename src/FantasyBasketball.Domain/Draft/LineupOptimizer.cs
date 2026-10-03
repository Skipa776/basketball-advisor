using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Schedule;

namespace FantasyBasketball.Domain.Draft;

/// <summary>
/// Season starting points for a roster on the NBA schedule: each lineup period (a day, or a
/// week for weekly leagues) the highest-value players with games fill the starting slots, each
/// into the most restrictive open slot it fits, or by an augmenting path when none is open — exact
/// for this vertex-weighted matching. Runs in binary floating point: it sits inside
/// thousands of rollouts per board request (draft_simulation, p95 under 2 s).
/// </summary>
public sealed class LineupOptimizer
{
    private readonly RosterSlot[] slots;
    private readonly SeasonSchedule schedule;
    private readonly LineupCadence cadence;
    private readonly int allSlots;

    public LineupOptimizer(IEnumerable<RosterSlot> starters, SeasonSchedule schedule, LineupCadence cadence)
    {
        ArgumentNullException.ThrowIfNull(starters);
        // Most restrictive first, so the lowest open bit a player fits is the tightest slot.
        slots = starters.Where(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR)
            .OrderBy(slot => Positions.Count(slot.Accepts))
            .ToArray();
        if (slots.Length is 0 or > 30)
        {
            throw new ArgumentException("A lineup needs 1–30 starting slots.", nameof(starters));
        }

        this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        this.cadence = cadence;
        allSlots = (1 << slots.Length) - 1;
    }

    private static readonly string[] Positions = ["PG", "SG", "SF", "PF", "C"];

    public SeasonSchedule Schedule => schedule;

    public int StarterCount => slots.Length;

    /// <summary>The starting slots a player with these positions can fill, as a bitmask.</summary>
    public int SlotMask(IReadOnlyList<string> positions) =>
        slots.Select((slot, index) => positions.Any(slot.Accepts) ? 1 << index : 0).Aggregate(0, (mask, bit) => mask | bit);

    /// <summary>
    /// Points from the roster's starters over the season. <paramref name="perGame"/> is each
    /// player's value per scheduled game (availability already folded in).
    /// </summary>
    public decimal Score(ReadOnlySpan<double> perGame, ReadOnlySpan<int> team, ReadOnlySpan<int> slotMask)
    {
        var count = perGame.Length;
        Span<int> order = stackalloc int[count];
        Span<int> playing = stackalloc int[count];
        Span<int> owner = stackalloc int[slots.Length];
        var total = 0.0;
        if (cadence == LineupCadence.Daily)
        {
            SortByValue(perGame, order);
            for (var day = 0; day < schedule.Days; day++)
            {
                var n = 0;
                foreach (var player in order)
                {
                    if (schedule.Plays[team[player]][day])
                    {
                        playing[n++] = player;
                    }
                }

                total += (double)Fill(perGame, slotMask, playing[..n], owner);
            }
        }
        else
        {
            Span<double> weekly = stackalloc double[count];
            for (var week = 0; week < schedule.Weeks; week++)
            {
                for (var i = 0; i < count; i++)
                {
                    weekly[i] = perGame[i] * schedule.GamesByWeek[team[i]][week];
                }

                SortByValue(weekly, order);
                total += (double)Fill(weekly, slotMask, order, owner);
            }
        }

        return (decimal)total;
    }

    /// <summary>One lineup period, exposed for the exactness test.</summary>
    public decimal AssignOnce(ReadOnlySpan<double> values, ReadOnlySpan<int> slotMask)
    {
        Span<int> order = stackalloc int[values.Length];
        Span<int> owner = stackalloc int[slots.Length];
        SortByValue(values, order);
        return Fill(values, slotMask, order, owner);
    }

    /// <summary>
    /// Players in descending value each join if an augmenting path frees a slot for them —
    /// the matroid greedy, so the lineup is the exact best for vertex-weighted matching.
    /// Most players take an open slot directly and never search.
    /// </summary>
    private decimal Fill(ReadOnlySpan<double> values, ReadOnlySpan<int> slotMask, ReadOnlySpan<int> order, Span<int> owner)
    {
        owner.Fill(-1);
        var open = allSlots;
        var total = 0.0;
        foreach (var player in order)
        {
            if (open == 0 || values[player] <= 0)
            {
                break;
            }

            var fits = slotMask[player] & open;
            if (fits != 0)
            {
                var bit = fits & -fits;
                owner[System.Numerics.BitOperations.TrailingZeroCount(bit)] = player;
                open &= ~bit;
                total += values[player];
            }
            else
            {
                var visited = 0;
                if (Augment(player, slotMask, owner, ref visited, ref open))
                {
                    total += values[player];
                }
            }
        }

        return (decimal)total;
    }

    private static bool Augment(int player, ReadOnlySpan<int> slotMask, Span<int> owner, ref int visited, ref int open)
    {
        var candidates = slotMask[player] & ~visited;
        while (candidates != 0)
        {
            var bit = candidates & -candidates;
            candidates &= ~bit;
            visited |= bit;
            var slot = System.Numerics.BitOperations.TrailingZeroCount(bit);
            var holder = owner[slot];
            if (holder < 0 || Augment(holder, slotMask, owner, ref visited, ref open))
            {
                owner[slot] = player;
                open &= ~bit;
                return true;
            }
        }

        return false;
    }

    private static void SortByValue(ReadOnlySpan<double> values, Span<int> order)
    {
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        var copy = values.ToArray();
        order.Sort((a, b) => copy[b].CompareTo(copy[a]));
    }
}
