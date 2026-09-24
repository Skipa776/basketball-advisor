using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Streaming;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Streaming;

public sealed class StreamingTests
{
    // Monday 2026-03-02 through Sunday 2026-03-08.
    private static readonly DateOnly[] Week = Enumerable.Range(0, 7).Select(offset => new DateOnly(2026, 3, 2).AddDays(offset)).ToArray();
    private static readonly RosterSlot Util = new(RosterSlotKind.UTIL);
    private static readonly RosterSlot Bench = new(RosterSlotKind.BENCH);

    private static StreamingPlayer Player(string name, string position, decimal perGame, params int[] days) =>
        new(new PlayerId(Guid.Parse($"00000000-0000-0000-0000-{Math.Abs(name.GetHashCode()) % 1_000_000_000_000:D12}")),
            name, [position], perGame, perGame * 20m, days.Select(day => Week[day]).ToHashSet());

    [Fact]
    public void S20_three_games_on_quiet_nights_beat_four_on_full_nights()
    {
        var incumbent = Player("Incumbent", "PG", 50m, 0, 1, 2, 3);
        var idle = Player("Idle", "SG", 1m);
        var fullNights = Player("Four Games", "SF", 20m, 0, 1, 2, 3);
        var quietNights = Player("Three Games", "PF", 20m, 4, 5, 6);

        var plan = StreamingPlanner.Plan([incumbent, idle], [fullNights, quietNights], [Util, Bench], LineupCadence.Daily, Week, 1);

        var move = plan.Moves.ShouldHaveSingleItem();
        move.Add.Name.ShouldBe("Three Games");
        move.Gain.ShouldBe(60m);
        move.AddUsableDays.Count.ShouldBe(3);
    }

    [Fact]
    public void S21_daily_assignment_never_overfills_or_starts_an_ineligible_player()
    {
        RosterSlot[] slots = [new(RosterSlotKind.PG), new(RosterSlotKind.C), Bench];
        StreamingPlayer[] roster = [Player("PG One", "PG", 30m, 0), Player("PG Two", "PG", 25m, 0), Player("Center", "C", 20m, 0), Player("Wing", "SF", 40m, 0)];

        var games = UsableGameCalculator.Daily(roster, slots, [Week[0]]);

        games.Count(game => game.Usable).ShouldBe(2);
        games.Where(game => game.Usable).GroupBy(game => game.Slot).ShouldAllBe(group => group.Count() == 1);
        games.Where(game => game.Usable).ShouldAllBe(game =>
            slots.Single(slot => slot.Kind == game.Slot).Accepts(roster.Single(player => player.Id == game.PlayerId).Positions[0]));
        games.Single(game => roster.Single(player => player.Id == game.PlayerId).Name == "Wing").Reason.ShouldBe("No eligible lineup slot");
        games.Single(game => roster.Single(player => player.Id == game.PlayerId).Name == "PG Two").Reason.ShouldContain("taken by PG One");
    }

    [Fact]
    public void S22_most_constrained_player_is_placed_first()
    {
        // The center fits C or UTIL; the forward fits only UTIL. Value-first greed would start one of them.
        var center = Player("Big Scorer", "C", 50m, 0);
        var forward = Player("Forward", "PF", 20m, 0);

        var games = UsableGameCalculator.Daily([center, forward], [new(RosterSlotKind.C), Util], [Week[0]]);

        games.ShouldAllBe(game => game.Usable);
        games.Single(game => game.PlayerId == forward.Id).Slot.ShouldBe(RosterSlotKind.UTIL);
    }

    [Fact]
    public void S23_plan_never_exceeds_the_acquisition_limit_and_reports_usage()
    {
        var roster = Enumerable.Range(0, 8).Select(index => Player($"Bench {index}", "SG", 1m)).Append(Player("Starter", "PG", 5m, 0)).ToArray();
        var streamers = Enumerable.Range(0, 10).Select(index => Player($"Streamer {index}", "SF", 30m + index, index % 7)).ToArray();
        RosterSlot[] slots = [Util, Util, Util, Bench, Bench, Bench, Bench, Bench, Bench];

        var plan = StreamingPlanner.Plan(roster, streamers, slots, LineupCadence.Daily, Week, 4);

        plan.Moves.Count.ShouldBe(4);
        plan.Moves.Select(move => move.AcquisitionsUsed).ShouldBe([1, 2, 3, 4]);
        plan.AcquisitionLimit.ShouldBe(4);
        plan.PlannedValue.ShouldBeGreaterThan(plan.BaselineValue);
    }

    [Fact]
    public void S24_moves_never_drop_an_ir_player_or_add_someone_already_rostered()
    {
        var injured = Player("Injured", "SG", 0m) with { OnInjuredReserve = true, RestOfSeasonValue = 0m };
        var starter = Player("Starter", "PG", 30m, 0, 1, 2, 3, 4, 5, 6) with { RestOfSeasonValue = 100_000m };

        var plan = StreamingPlanner.Plan([starter, injured], [starter, Player("Streamer", "SF", 40m, 0, 1)],
            [Util, new(RosterSlotKind.IR)], LineupCadence.Daily, Week, 7);

        plan.Moves.ShouldBeEmpty("the IR occupant cannot be dropped and the starter is protected");
    }

    [Fact]
    public void S25_a_valuable_rostered_player_is_not_dropped_for_a_small_gain()
    {
        var valuable = Player("Resting Star", "C", 60m) with { RestOfSeasonValue = 3000m };
        var plan = StreamingPlanner.Plan([valuable], [Player("Streamer", "PG", 10m, 0, 1)], [Util], LineupCadence.Daily, Week, 7);

        plan.Moves.ShouldBeEmpty("3000 > 1.5 × 20");
    }

    [Fact]
    public void S26_a_day_with_no_open_slots_yields_zero_usable_games()
    {
        var incumbent = Player("Incumbent", "PG", 50m, 0);
        var extra = Player("Extra", "SG", 40m, 0);

        var games = UsableGameCalculator.Daily([incumbent, extra], [Util], [Week[0]]);

        games.Single(game => game.PlayerId == extra.Id).Usable.ShouldBeFalse();
        StreamingPlanner.Value([incumbent, extra], [Util], LineupCadence.Daily, [Week[0]], []).ShouldBe(50m);
    }

    [Fact]
    public void S27_missing_opponent_data_means_a_neutral_adjustment_with_evidence()
    {
        var plan = StreamingPlanner.Plan([Player("Idle", "SG", 1m)], [Player("Streamer", "PG", 10m, 0)], [Util], LineupCadence.Daily, Week, 1);

        plan.MatchupAdjustment.ShouldBe(1.0m);
        plan.Evidence.ShouldContain(item => item.Contains("opponent", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void S28_identical_inputs_give_identical_plans()
    {
        StreamingPlayer[] roster = [Player("Idle A", "SG", 1m), Player("Idle B", "SF", 1m)];
        StreamingPlayer[] pool = [Player("Twin A", "PG", 20m, 0, 2), Player("Twin B", "PG", 20m, 0, 2)];

        var first = StreamingPlanner.Plan(roster, pool, [Util, Bench], LineupCadence.Daily, Week, 2);
        var second = StreamingPlanner.Plan(roster.Reverse().ToArray(), pool.Reverse().ToArray(), [Util, Bench], LineupCadence.Daily, Week, 2);

        second.Moves.Select(move => (move.Day, move.Add.Id, move.Drop.Id))
            .ShouldBe(first.Moves.Select(move => (move.Day, move.Add.Id, move.Drop.Id)));
    }

    [Fact]
    public void Weekly_leagues_count_all_games_of_the_chosen_starters_only()
    {
        var busy = Player("Four Games", "PG", 20m, 0, 1, 2, 3);
        var light = Player("Two Games", "SG", 30m, 5, 6);

        var games = UsableGameCalculator.Weekly([busy, light], [Util], Week);

        games.Where(game => game.Usable).ShouldAllBe(game => game.PlayerId == busy.Id, "80 beats 60 for the week");
        games.Count(game => game.Usable).ShouldBe(4);
    }
}
