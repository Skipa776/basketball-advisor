using FantasyBasketball.Domain.Leagues;

namespace FantasyBasketball.Domain.Streaming;

public sealed record StreamingMove(
    DateOnly Day,
    StreamingPlayer Add,
    StreamingPlayer Drop,
    decimal Gain,
    IReadOnlyList<DateOnly> AddUsableDays,
    IReadOnlyList<DateOnly> DropUsableDaysLost,
    int AcquisitionsUsed);

public sealed record StreamingPlan(
    IReadOnlyList<StreamingMove> Moves,
    decimal BaselineValue,
    decimal PlannedValue,
    int AcquisitionLimit,
    decimal MatchupAdjustment,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Greedy over decision days plus one improvement pass (streaming_contract). A move's
/// gain is the change in the whole lineup's usable value from its day to the end of
/// the horizon, so slot competition is always accounted for. Illegal or protected
/// drops are never scored.
/// </summary>
public static class StreamingPlanner
{
    public const decimal KeepMultiple = 1.5m;

    /// <summary>No opponent defensive data is stored; the contract's value when it is absent.</summary>
    public const decimal NoOpponentDataAdjustment = 1.0m;

    public static StreamingPlan Plan(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<StreamingPlayer> freeAgents,
        IReadOnlyList<RosterSlot> slots,
        LineupCadence cadence,
        IReadOnlyList<DateOnly> horizon,
        int acquisitionLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(acquisitionLimit);
        var rostered = roster.Select(player => player.Id).ToHashSet();
        var pool = freeAgents.Where(player => !rostered.Contains(player.Id)).DistinctBy(player => player.Id).ToArray();
        // Weekly leagues lock the lineup once, so only the first day can change it.
        var decisionDays = cadence == LineupCadence.Weekly ? horizon.Take(1).ToArray() : horizon.ToArray();
        var moves = new List<StreamingMove>();

        foreach (var day in decisionDays)
        {
            while (moves.Count < acquisitionLimit && BestMove(roster, pool, slots, cadence, horizon, moves, day) is { } best)
            {
                moves.Add(best with { AcquisitionsUsed = moves.Count + 1 });
            }
        }

        // One improvement pass: try the best alternative for each move's day, keep it if the plan improves.
        for (var index = 0; index < moves.Count; index++)
        {
            var others = moves.Where((_, position) => position != index).ToList();
            var current = Value(roster, slots, cadence, horizon, moves);
            if (BestMove(roster, pool, slots, cadence, horizon, others, moves[index].Day) is { } alternative)
            {
                var swapped = others.Append(alternative).OrderBy(move => move.Day).ToList();
                if (Value(roster, slots, cadence, horizon, swapped) > current)
                {
                    moves = swapped;
                }
            }
        }

        var ordered = moves.OrderBy(move => move.Day).Select((move, index) => move with { AcquisitionsUsed = index + 1 }).ToArray();
        return new StreamingPlan(ordered, Value(roster, slots, cadence, horizon, []), Value(roster, slots, cadence, horizon, ordered),
            acquisitionLimit, NoOpponentDataAdjustment,
            [
                "No opponent defensive data is stored, so every matchup adjustment is 1.0.",
                "No injury feed: every scheduled game is assumed available.",
            ]);
    }

    /// <summary>Total usable value of a week: Σ per-game value × role confidence over usable games.</summary>
    public static decimal Value(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<RosterSlot> slots,
        LineupCadence cadence,
        IReadOnlyList<DateOnly> horizon,
        IReadOnlyList<StreamingMove> moves) =>
        horizon.Sum(day =>
        {
            var lineup = RosterOn(roster, moves, day);
            var byId = lineup.ToDictionary(player => player.Id);
            var games = cadence == LineupCadence.Weekly
                ? UsableGameCalculator.Weekly(RosterOn(roster, moves, horizon[0]), slots, horizon).Where(game => game.Day == day)
                : UsableGameCalculator.Daily(lineup, slots, [day]);
            return games.Where(game => game.Usable && byId.ContainsKey(game.PlayerId))
                .Sum(game => byId[game.PlayerId].PerGameValue * byId[game.PlayerId].RoleConfidence * NoOpponentDataAdjustment);
        });

    private static StreamingMove? BestMove(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<StreamingPlayer> pool,
        IReadOnlyList<RosterSlot> slots,
        LineupCadence cadence,
        IReadOnlyList<DateOnly> horizon,
        IReadOnlyList<StreamingMove> plan,
        DateOnly day)
    {
        var current = RosterOn(roster, plan, day);
        var taken = plan.Select(move => move.Add.Id).ToHashSet();
        var remaining = horizon.Where(date => date >= day).ToArray();
        var baseline = Value(roster, slots, cadence, horizon, plan);
        StreamingMove? best = null;
        foreach (var add in pool.Where(player => !taken.Contains(player.Id)).OrderBy(player => player.Id.Value))
        {
            foreach (var drop in current.Where(player => !player.OnInjuredReserve).OrderBy(player => player.Id.Value))
            {
                var candidate = new StreamingMove(day, add, drop, 0m, [], [], 0);
                var gain = Value(roster, slots, cadence, horizon, [.. plan, candidate]) - baseline;
                // Drop protection: a rostered contributor is worth more than a short-horizon bet.
                if (gain <= 0m || drop.RestOfSeasonValue > KeepMultiple * gain || (best is not null && gain <= best.Gain))
                {
                    continue;
                }

                var after = RosterOn(roster, [.. plan, candidate], day);
                best = candidate with
                {
                    Gain = gain,
                    AddUsableDays = UsableDays(after, add, slots, cadence, remaining),
                    DropUsableDaysLost = UsableDays(current, drop, slots, cadence, remaining),
                };
            }
        }

        return best;
    }

    private static IReadOnlyList<DateOnly> UsableDays(
        IReadOnlyList<StreamingPlayer> lineup, StreamingPlayer player, IReadOnlyList<RosterSlot> slots,
        LineupCadence cadence, IReadOnlyList<DateOnly> days) =>
        UsableGameCalculator.Calculate(lineup, slots, cadence, days)
            .Where(game => game.Usable && game.PlayerId == player.Id)
            .Select(game => game.Day)
            .Distinct()
            .ToArray();

    // The roster on a day: every move dated on or before it has happened.
    private static IReadOnlyList<StreamingPlayer> RosterOn(
        IReadOnlyList<StreamingPlayer> roster, IReadOnlyList<StreamingMove> moves, DateOnly day)
    {
        var current = roster.ToList();
        foreach (var move in moves.Where(move => move.Day <= day).OrderBy(move => move.Day))
        {
            current.RemoveAll(player => player.Id == move.Drop.Id);
            current.Add(move.Add);
        }

        return current;
    }
}
