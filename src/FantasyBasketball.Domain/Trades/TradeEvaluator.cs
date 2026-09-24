using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Trades;

public sealed record TradePlayer(PlayerId Id, string Name, IReadOnlyList<string> Positions, decimal? SeasonValue);

/// <summary>One player moving from one team to another. A multi-team trade is several legs.</summary>
public sealed record TradeLeg(Guid FromTeamId, Guid ToTeamId, PlayerId PlayerId);

public enum TradeVerdict
{
    ClearLoss,
    SlightLoss,
    Neutral,
    SlightWin,
    ClearWin,
}

public sealed record TradeLineup(decimal Value, IReadOnlyList<PlayerId> Starters, int BackfilledSlots);

public sealed record TradeEvaluation(
    decimal ValueBefore,
    decimal ValueAfter,
    decimal Delta,
    TradeVerdict Verdict,
    string Confidence,
    TradeLineup Before,
    TradeLineup After,
    IReadOnlyList<string> Evidence);

/// <summary>An illegal trade is refused by naming the rule, never scored (trade_contract).</summary>
public sealed class TradeRuleException(string rule) : Exception(rule);

/// <summary>
/// Points-league trade evaluation from the user's side only: the value of the best legal
/// starting lineup before and after, with open starting slots filled at replacement value.
/// </summary>
public static class TradeEvaluator
{
    public const decimal NeutralBand = 0.02m;
    public const decimal SlightBand = 0.08m;
    private static readonly string[] AllPositions = ["PG", "SG", "SF", "PF", "C"];

    public static TradeEvaluation Evaluate(
        Guid userTeamId,
        IReadOnlyDictionary<Guid, IReadOnlyList<TradePlayer>> rosters,
        IReadOnlyList<TradeLeg> legs,
        IReadOnlyList<RosterSlot> slots,
        decimal replacementValue)
    {
        Validate(userTeamId, rosters, legs, slots);
        var before = rosters[userTeamId];
        var byId = rosters.Values.SelectMany(roster => roster).ToDictionary(player => player.Id);
        var after = before.Where(player => !legs.Any(leg => leg.FromTeamId == userTeamId && leg.PlayerId == player.Id))
            .Concat(legs.Where(leg => leg.ToTeamId == userTeamId).Select(leg => byId[leg.PlayerId]))
            .ToArray();

        var lineupBefore = StartingLineup(before, slots, replacementValue);
        var lineupAfter = StartingLineup(after, slots, replacementValue);
        var delta = lineupAfter.Value - lineupBefore.Value;
        var involved = legs.Select(leg => byId[leg.PlayerId]).ToArray();
        return new TradeEvaluation(lineupBefore.Value, lineupAfter.Value, delta, Verdict(delta, lineupBefore.Value),
            Confidence(involved), lineupBefore, lineupAfter,
            Evidence(userTeamId, legs, byId, lineupBefore, lineupAfter, involved, replacementValue));
    }

    /// <summary>Best legal starting assignment: most-constrained first, then value, then id.</summary>
    public static TradeLineup StartingLineup(IReadOnlyList<TradePlayer> roster, IReadOnlyList<RosterSlot> slots, decimal replacementValue)
    {
        // ponytail: greedy with restriction ordering (as streaming); exact matching if a real case shows it losing value.
        var open = slots.Where(slot => slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR)
            .OrderBy(slot => AllPositions.Count(slot.Accepts)).ThenBy(slot => slot.Kind).ToList();
        var starters = new List<PlayerId>();
        var value = 0m;
        foreach (var player in roster.Where(player => player.SeasonValue > 0m)
                     .OrderBy(player => open.Count(slot => player.Positions.Any(slot.Accepts)))
                     .ThenByDescending(player => player.SeasonValue)
                     .ThenBy(player => player.Id.Value))
        {
            if (open.FirstOrDefault(slot => player.Positions.Any(slot.Accepts)) is { } slot)
            {
                open.Remove(slot);
                starters.Add(player.Id);
                value += player.SeasonValue!.Value;
            }
        }

        // Replacement backfill: an empty starting slot is filled from waivers at replacement level.
        return new TradeLineup(value + open.Count * replacementValue, starters, open.Count);
    }

    public static TradeVerdict Verdict(decimal delta, decimal valueBefore)
    {
        var ratio = valueBefore == 0m ? (delta == 0m ? 0m : 1m) : Math.Abs(delta) / valueBefore;
        return ratio < NeutralBand ? TradeVerdict.Neutral
            : ratio < SlightBand ? (delta > 0m ? TradeVerdict.SlightWin : TradeVerdict.SlightLoss)
            : delta > 0m ? TradeVerdict.ClearWin : TradeVerdict.ClearLoss;
    }

    private static void Validate(Guid userTeamId, IReadOnlyDictionary<Guid, IReadOnlyList<TradePlayer>> rosters,
        IReadOnlyList<TradeLeg> legs, IReadOnlyList<RosterSlot> slots)
    {
        if (legs.Count == 0)
        {
            throw new TradeRuleException("A trade needs at least one player moving.");
        }

        if (!rosters.ContainsKey(userTeamId) || !legs.Any(leg => leg.FromTeamId == userTeamId || leg.ToTeamId == userTeamId))
        {
            throw new TradeRuleException("Your team must send or receive a player.");
        }

        if (legs.Select(leg => leg.PlayerId).Distinct().Count() != legs.Count)
        {
            throw new TradeRuleException("A player can move only once in a trade.");
        }

        foreach (var leg in legs)
        {
            if (leg.FromTeamId == leg.ToTeamId || !rosters.ContainsKey(leg.ToTeamId)
                || !rosters.TryGetValue(leg.FromTeamId, out var from) || from.All(player => player.Id != leg.PlayerId))
            {
                throw new TradeRuleException("Every player must come from the roster that sends him, to another team in the league.");
            }
        }

        var rosterLimit = slots.Count(slot => slot.Kind != RosterSlotKind.IR);
        foreach (var (teamId, roster) in rosters)
        {
            var size = roster.Count - legs.Count(leg => leg.FromTeamId == teamId) + legs.Count(leg => leg.ToTeamId == teamId);
            if (size > rosterLimit)
            {
                throw new TradeRuleException($"A roster would hold {size} players; the league allows {rosterLimit}.");
            }
        }

        var byId = rosters.Values.SelectMany(roster => roster).ToDictionary(player => player.Id);
        var homeless = legs.Where(leg => leg.ToTeamId == userTeamId)
            .Select(leg => byId[leg.PlayerId])
            .FirstOrDefault(player => !slots.Any(slot => player.Positions.Any(slot.Accepts)));
        if (homeless is not null)
        {
            throw new TradeRuleException($"{homeless.Name} has no position that fits a roster slot.");
        }
    }

    private static string Confidence(IReadOnlyList<TradePlayer> involved)
    {
        var missing = involved.Count(player => player.SeasonValue is null);
        return missing == 0 ? "High" : missing * 2 <= involved.Count ? "Moderate" : "Low";
    }

    private static IReadOnlyList<string> Evidence(Guid userTeamId, IReadOnlyList<TradeLeg> legs,
        IReadOnlyDictionary<PlayerId, TradePlayer> byId, TradeLineup before, TradeLineup after,
        IReadOnlyList<TradePlayer> involved, decimal replacementValue)
    {
        var evidence = new List<string>();
        foreach (var received in legs.Where(leg => leg.ToTeamId == userTeamId).Select(leg => byId[leg.PlayerId]))
        {
            evidence.Add(after.Starters.Contains(received.Id)
                ? $"{received.Name} would start."
                : $"{received.Name} would sit: your starting slots are already filled by better players (slot displacement).");
        }

        if (after.BackfilledSlots > before.BackfilledSlots)
        {
            evidence.Add($"{after.BackfilledSlots - before.BackfilledSlots} starting slot(s) would open and be filled from waivers at replacement value ({replacementValue:0.#}).");
        }

        foreach (var unknown in involved.Where(player => player.SeasonValue is null))
        {
            evidence.Add($"{unknown.Name} has no projection in this league, so he counts as zero.");
        }

        if (legs.Select(leg => leg.FromTeamId).Concat(legs.Select(leg => leg.ToTeamId)).Distinct().Count() > 2)
        {
            evidence.Add("Multi-team trade: the verdict is for your team only; other sides are shown for information.");
        }

        evidence.Add("The league's trade deadline is not stored, so the deadline is not checked.");
        evidence.Add("This judges what the trade does to your team, not whether it is fair to the other manager.");
        return evidence;
    }
}
