using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Streaming;

/// <summary>A player as the streaming engine sees him for one week.</summary>
public sealed record StreamingPlayer(
    PlayerId Id,
    string Name,
    IReadOnlyList<string> Positions,
    decimal PerGameValue,
    decimal RestOfSeasonValue,
    IReadOnlySet<DateOnly> GameDays,
    decimal RoleConfidence = 1m,
    bool OnInjuredReserve = false);

public sealed record UsableGame(PlayerId PlayerId, DateOnly Day, bool Usable, RosterSlotKind? Slot, string Reason);

/// <summary>
/// A game counts only when the player can actually be started that day
/// (streaming_contract). Daily leagues fill each day's open slots most-constrained
/// player first; weekly leagues choose starters once and count all their games.
/// </summary>
public static class UsableGameCalculator
{
    private static readonly string[] AllPositions = ["PG", "SG", "SF", "PF", "C"];

    public static IReadOnlyList<UsableGame> Calculate(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<RosterSlot> slots,
        LineupCadence cadence,
        IReadOnlyList<DateOnly> horizon) =>
        cadence == LineupCadence.Weekly ? Weekly(roster, slots, horizon) : Daily(roster, slots, horizon);

    public static IReadOnlyList<UsableGame> Daily(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<RosterSlot> slots,
        IReadOnlyList<DateOnly> horizon)
    {
        var games = new List<UsableGame>();
        foreach (var day in horizon)
        {
            var playing = roster.Where(player => !player.OnInjuredReserve && player.GameDays.Contains(day)).ToArray();
            games.AddRange(Assign(playing, slots, player => player.PerGameValue, day));
        }

        return games;
    }

    public static IReadOnlyList<UsableGame> Weekly(
        IReadOnlyList<StreamingPlayer> roster,
        IReadOnlyList<RosterSlot> slots,
        IReadOnlyList<DateOnly> horizon)
    {
        // The lineup is set once, so starters are chosen on their whole week.
        var days = horizon.ToHashSet();
        var eligible = roster.Where(player => !player.OnInjuredReserve).ToArray();
        var starters = Assign(eligible, slots, player => player.PerGameValue * player.GameDays.Count(days.Contains), null)
            .Where(game => game.Usable)
            .ToDictionary(game => game.PlayerId, game => game.Slot);
        return horizon.SelectMany(day => roster
                .Where(player => player.GameDays.Contains(day))
                .Select(player => starters.TryGetValue(player.Id, out var slot)
                    ? new UsableGame(player.Id, day, true, slot, $"Started all week at {slot}")
                    : new UsableGame(player.Id, day, false, null,
                        player.OnInjuredReserve ? "On IR" : "Benched for the week; the lineup is set once")))
            .ToArray();
    }

    private static IEnumerable<UsableGame> Assign(
        IReadOnlyList<StreamingPlayer> candidates,
        IReadOnlyList<RosterSlot> slots,
        Func<StreamingPlayer, decimal> value,
        DateOnly? day)
    {
        // Most specific slots first, so a flexible UTIL stays open for whoever needs it.
        var open = slots.Where(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR)
            .OrderBy(slot => AllPositions.Count(slot.Accepts))
            .ThenBy(slot => slot.Kind)
            .ToList();
        var filledBy = new Dictionary<RosterSlotKind, string>();
        // Most-constrained first, then value, then id for a deterministic plan.
        foreach (var player in candidates
                     .OrderBy(player => open.Count(slot => Eligible(slot, player)))
                     .ThenByDescending(value)
                     .ThenBy(player => player.Id.Value))
        {
            var slot = open.FirstOrDefault(candidate => Eligible(candidate, player));
            var gameDay = day ?? default;
            if (slot is not null)
            {
                open.Remove(slot);
                filledBy[slot.Kind] = player.Name;
                yield return new UsableGame(player.Id, gameDay, true, slot.Kind, $"Starts at {slot.Kind}");
                continue;
            }

            var eligibleKinds = slots.Where(candidate => candidate.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR
                && Eligible(candidate, player)).Select(candidate => candidate.Kind).Distinct().ToArray();
            yield return new UsableGame(player.Id, gameDay, false, null, player.Positions.Count == 0
                ? "No known position, so no eligible slot"
                : eligibleKinds.Length == 0
                    ? "No eligible lineup slot"
                    : $"No open {string.Join("/", eligibleKinds)} slot; taken by {string.Join(", ", eligibleKinds.Where(filledBy.ContainsKey).Select(kind => filledBy[kind]).Distinct())}");
        }
    }

    private static bool Eligible(RosterSlot slot, StreamingPlayer player) => player.Positions.Any(slot.Accepts);
}
