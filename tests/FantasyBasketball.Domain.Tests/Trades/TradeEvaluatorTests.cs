using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Trades;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Trades;

public sealed class TradeEvaluatorTests
{
    private static readonly Guid Mine = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Theirs = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Third = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private const decimal Replacement = 1000m;

    private static TradePlayer Player(string name, string position, decimal? value) =>
        new(new PlayerId(Guid.NewGuid()), name, [position], value);

    private static RosterSlot[] Slots(params RosterSlotKind[] kinds) => kinds.Select(kind => new RosterSlot(kind)).ToArray();

    [Fact]
    public void R01_two_for_one_credits_replacement_backfill()
    {
        var a = Player("A", "PG", 3000m);
        var b = Player("B", "SG", 2500m);
        var star = Player("Star", "PG", 4000m);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>> { [Mine] = [a, b], [Theirs] = [star] };

        var result = TradeEvaluator.Evaluate(Mine, rosters,
            [new(Mine, Theirs, a.Id), new(Mine, Theirs, b.Id), new(Theirs, Mine, star.Id)],
            Slots(RosterSlotKind.G, RosterSlotKind.G, RosterSlotKind.BENCH), Replacement);

        result.After.BackfilledSlots.ShouldBe(1);
        result.ValueAfter.ShouldBe(4000m + Replacement, "the vacated slot is filled at replacement level");
        result.Delta.ShouldBe(-500m);
        result.Evidence.ShouldContain(item => item.Contains("replacement value", StringComparison.Ordinal));
    }

    [Fact]
    public void R02_a_third_center_is_discounted_by_slot_displacement()
    {
        var c1 = Player("C1", "C", 3000m);
        var c2 = Player("C2", "C", 2800m);
        var guard = Player("Guard", "PG", 500m);
        var thirdCenter = Player("Third Center", "C", 2600m);
        var filler = Player("Filler", "SF", 100m);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>> { [Mine] = [c1, c2, guard], [Theirs] = [thirdCenter, filler] };

        var result = TradeEvaluator.Evaluate(Mine, rosters, [new(Mine, Theirs, guard.Id), new(Theirs, Mine, thirdCenter.Id)],
            Slots(RosterSlotKind.C, RosterSlotKind.C, RosterSlotKind.PG, RosterSlotKind.BENCH), Replacement);

        result.Delta.ShouldBe(500m, "he sits, the PG slot backfills at 1000 for the 500 guard");
        result.Delta.ShouldBeLessThan(thirdCenter.SeasonValue!.Value / 2m);
        result.Evidence.ShouldContain(item => item.Contains("Third Center would sit", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("stolen")]
    [InlineData("twice")]
    [InlineData("oversize")]
    [InlineData("uninvolved")]
    public void R03_an_illegal_trade_is_refused_not_scored(string rule)
    {
        var mine = Player("Mine", "PG", 1000m);
        var theirs = Player("Theirs", "SG", 1000m);
        var other = Player("Other", "SF", 1000m);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>> { [Mine] = [mine], [Theirs] = [theirs, other], [Third] = [] };
        TradeLeg[] legs = rule switch
        {
            "stolen" => [new(Theirs, Mine, mine.Id)],
            "twice" => [new(Theirs, Mine, theirs.Id), new(Theirs, Third, theirs.Id)],
            "oversize" => [new(Theirs, Mine, theirs.Id), new(Theirs, Mine, other.Id)],
            _ => [new(Theirs, Third, theirs.Id)],
        };

        Should.Throw<TradeRuleException>(() => TradeEvaluator.Evaluate(Mine, rosters, legs,
            Slots(RosterSlotKind.UTIL, RosterSlotKind.BENCH), Replacement));
    }

    [Fact]
    public void R05_swapping_equal_value_is_exactly_neutral()
    {
        var mine = Player("Mine", "PG", 2000m);
        var twin = Player("Twin", "PG", 2000m);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>> { [Mine] = [mine], [Theirs] = [twin] };

        var result = TradeEvaluator.Evaluate(Mine, rosters, [new(Mine, Theirs, mine.Id), new(Theirs, Mine, twin.Id)],
            Slots(RosterSlotKind.PG), Replacement);

        result.Delta.ShouldBe(0m);
        result.Verdict.ShouldBe(TradeVerdict.Neutral);
    }

    [Fact]
    public void R06_multi_team_trade_has_one_verdict_for_the_user()
    {
        var mine = Player("Mine", "PG", 1000m);
        var fromTheirs = Player("From Theirs", "PG", 3000m);
        var fromThird = Player("From Third", "SF", 500m);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>>
        {
            [Mine] = [mine],
            [Theirs] = [fromTheirs],
            [Third] = [fromThird],
        };

        var result = TradeEvaluator.Evaluate(Mine, rosters,
            [new(Mine, Third, mine.Id), new(Theirs, Mine, fromTheirs.Id), new(Third, Theirs, fromThird.Id)],
            Slots(RosterSlotKind.PG, RosterSlotKind.BENCH), Replacement);

        result.Verdict.ShouldBe(TradeVerdict.ClearWin);
        result.Evidence.ShouldContain(item => item.StartsWith("Multi-team trade", StringComparison.Ordinal));
    }

    [Fact]
    public void R07_every_evaluation_carries_evidence_and_confidence()
    {
        var mine = Player("Mine", "PG", 1000m);
        var unknown = Player("Rookie", "PG", null);
        var rosters = new Dictionary<Guid, IReadOnlyList<TradePlayer>> { [Mine] = [mine], [Theirs] = [unknown] };

        var result = TradeEvaluator.Evaluate(Mine, rosters, [new(Mine, Theirs, mine.Id), new(Theirs, Mine, unknown.Id)],
            Slots(RosterSlotKind.PG), Replacement);

        result.Confidence.ShouldBe("Moderate");
        result.Evidence.ShouldContain(item => item.Contains("no projection", StringComparison.Ordinal));
        result.Evidence.ShouldContain(item => item.Contains("not whether it is fair", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(10, 1000, TradeVerdict.Neutral)]
    [InlineData(50, 1000, TradeVerdict.SlightWin)]
    [InlineData(-50, 1000, TradeVerdict.SlightLoss)]
    [InlineData(-100, 1000, TradeVerdict.ClearLoss)]
    [InlineData(80, 1000, TradeVerdict.ClearWin)]
    public void Verdict_bands_follow_the_contract(int delta, int before, TradeVerdict verdict) =>
        TradeEvaluator.Verdict(delta, before).ShouldBe(verdict);
}
