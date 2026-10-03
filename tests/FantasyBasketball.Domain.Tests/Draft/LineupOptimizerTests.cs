using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Draft;

public sealed class LineupOptimizerTests
{
    private static readonly NbaTeamId Home = new(Guid.NewGuid());
    private static readonly NbaTeamId Away = new(Guid.NewGuid());
    private static readonly NbaTeamId Other = new(Guid.NewGuid());

    private static readonly RosterSlot[] Standard =
    [
        new(RosterSlotKind.PG), new(RosterSlotKind.SG), new(RosterSlotKind.SF), new(RosterSlotKind.PF), new(RosterSlotKind.C),
        new(RosterSlotKind.G), new(RosterSlotKind.F), new(RosterSlotKind.UTIL), new(RosterSlotKind.UTIL), new(RosterSlotKind.UTIL),
    ];

    [Fact]
    public void LO01_lineup_assignment_matches_exact_matching()
    {
        var optimizer = new LineupOptimizer(Standard, Schedule((1, Home, Away)), LineupCadence.Daily);
        string[][] eligibility = [["PG"], ["SG"], ["SF"], ["PF"], ["C"], ["PG", "SG"], ["SG", "SF"], ["SF", "PF"], ["PF", "C"]];
        var random = new Random(41);
        for (var trial = 0; trial < 500; trial++)
        {
            var count = random.Next(8, 14);
            var values = Enumerable.Range(0, count).Select(_ => random.NextDouble() * 50).ToArray();
            var masks = Enumerable.Range(0, count).Select(_ => optimizer.SlotMask(eligibility[random.Next(eligibility.Length)])).ToArray();

            var greedy = optimizer.AssignOnce(values, masks);
            var exact = Exact(values, masks, optimizer.StarterCount);

            greedy.ShouldBe(exact, 0.0000001m, $"trial {trial}");
        }
    }

    [Fact]
    public void LO02_daily_lineups_count_each_scheduled_day_for_the_best_eligible_players()
    {
        // Home plays days 1-3, Other plays day 4: one UTIL slot.
        var schedule = Schedule((1, Home, Away), (2, Home, Away), (3, Away, Home), (4, Other, Away));
        var optimizer = new LineupOptimizer([new RosterSlot(RosterSlotKind.UTIL)], schedule, LineupCadence.Daily);
        var mask = optimizer.SlotMask(["C"]);

        var score = optimizer.Score([30.0, 20.0, 10.0], [schedule.TeamIndex(Home), schedule.TeamIndex(Home), schedule.TeamIndex(Other)], [mask, mask, mask]);

        score.ShouldBe((3 * 30m) + 10m);
    }

    [Fact]
    public void LO03_weekly_lineups_weight_values_by_games_that_week()
    {
        // Week 1: Home 2 games, Other 1 game. Week 2 (day 8): only Other plays.
        var schedule = Schedule((1, Home, Away), (2, Home, Away), (3, Other, Away), (8, Other, Away));
        var optimizer = new LineupOptimizer([new RosterSlot(RosterSlotKind.UTIL)], schedule, LineupCadence.Weekly);
        var mask = optimizer.SlotMask(["C"]);

        var score = optimizer.Score([30.0, 50.0], [schedule.TeamIndex(Home), schedule.TeamIndex(Other)], [mask, mask]);

        score.ShouldBe((2 * 30m) + 50m, "week 1: 60 beats 50; week 2: Home has no games");
    }

    [Fact]
    public void LO04_game_days_are_eastern_dates_and_unknown_teams_get_a_typical_schedule()
    {
        var late = new DateTimeOffset(2025, 10, 22, 2, 30, 0, TimeSpan.Zero); // 10:30 pm Eastern on the 21st
        var schedule = new SeasonSchedule([Game(new DateTimeOffset(2025, 10, 21, 23, 0, 0, TimeSpan.Zero), Home, Away), Game(late, Other, Away)]);

        schedule.Days.ShouldBe(1);
        schedule.Games(schedule.TeamIndex(Away)).ShouldBe(1);
        schedule.TeamIndex(null).ShouldBe(schedule.TypicalTeam);
    }

    /// <summary>Exact best lineup by dynamic programming over used-slot masks.</summary>
    private static decimal Exact(double[] values, int[] masks, int slotCount)
    {
        var best = new Dictionary<int, double> { [0] = 0 };
        for (var player = 0; player < values.Length; player++)
        {
            var next = new Dictionary<int, double>(best);
            foreach (var (used, total) in best)
            {
                for (var slot = 0; slot < slotCount; slot++)
                {
                    var bit = 1 << slot;
                    if ((masks[player] & bit) != 0 && (used & bit) == 0)
                    {
                        var key = used | bit;
                        var candidate = total + values[player];
                        if (!next.TryGetValue(key, out var current) || candidate > current)
                        {
                            next[key] = candidate;
                        }
                    }
                }
            }

            best = next;
        }

        return (decimal)best.Values.Max();
    }

    private static SeasonSchedule Schedule(params (int Day, NbaTeamId Home, NbaTeamId Away)[] games) =>
        new([.. games.Select(game => Game(new DateTimeOffset(2025, 10, 20, 23, 0, 0, TimeSpan.Zero).AddDays(game.Day), game.Home, game.Away))]);

    private static NbaGame Game(DateTimeOffset startsAt, NbaTeamId home, NbaTeamId away) => new(
        Guid.NewGuid(), 2026, startsAt, home, away, null, null, "scheduled",
        new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64)));
}
