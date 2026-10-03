using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Schedule;
using FantasyBasketball.Domain.Statistics;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Draft;

public sealed class DraftSimulatorTests
{
    private static readonly NbaTeamId Team = new(Guid.NewGuid());
    private static readonly SeasonSchedule EightyTwoDays = Schedule(82);
    private static readonly DraftSimulator Simulator = new(new SimulationOptions { Candidates = 4, Rollouts = 400 });

    [Fact]
    public void DS01_same_seed_and_state_give_the_same_board()
    {
        var (session, lineup, pool) = ScarceCenter();

        var first = Simulator.Simulate(session, lineup, pool, 11, RiskMode.Mean);
        var second = Simulator.Simulate(session, lineup, pool, 11, RiskMode.Mean);

        second.Candidates.ShouldBe(first.Candidates);
    }

    [Fact]
    public void DS02_common_random_numbers_make_the_edge_tighter_than_independent_draws()
    {
        var (session, lineup, pool) = ScarceCenter();

        var board = Simulator.Simulate(session, lineup, pool, 5, RiskMode.Mean);

        var runnerUp = board.Candidates[1];
        var independentSe = (decimal)Math.Sqrt((double)((runnerUp.Sd * runnerUp.Sd) + (board.Candidates[0].Sd * board.Candidates[0].Sd)) / 400);
        runnerUp.EdgeSe.ShouldBeLessThan(independentSe);
        board.Candidates[0].Edge.ShouldBe(0m);
    }

    [Fact]
    public void DS03_takes_the_scarce_center_over_a_slightly_better_guard()
    {
        // Slots C + PG, two teams, user picks 1 and 4. Take PG1 now and the opponent takes C1 and
        // PG2, leaving C2 (20): about 125. Take C1 and PG3 (103) is still there at pick 4: about 203.
        var (session, lineup, pool) = ScarceCenter();

        var board = Simulator.Simulate(session, lineup, pool, 3, RiskMode.Mean);

        board.Candidates[0].PlayerId.ShouldBe(pool[0].PlayerId, "C1 is the pick even though PG1 projects higher");
        board.Candidates.Single(candidate => candidate.PlayerId == pool[1].PlayerId).Edge.ShouldBeLessThan(-50m);
    }

    [Fact]
    public void DS04_cautious_and_upside_modes_split_equal_means_by_spread()
    {
        var session = new DraftSession(Guid.NewGuid(), 2, 1, 1);
        var lineup = new LineupOptimizer([new RosterSlot(RosterSlotKind.UTIL)], EightyTwoDays, LineupCadence.Daily);
        DraftCandidate[] pool = [Candidate("steady", 100m, 0.01m, ["C"], 1m), Candidate("volatile", 100m, 40m, ["C"], 2m)];

        var cautious = Simulator.Simulate(session, lineup, pool, 1, RiskMode.Cautious);
        var upside = Simulator.Simulate(session, lineup, pool, 1, RiskMode.Upside);

        cautious.Candidates[0].PlayerId.ShouldBe(pool[0].PlayerId);
        upside.Candidates[0].PlayerId.ShouldBe(pool[1].PlayerId);
        upside.Candidates[0].P90.ShouldBeGreaterThan(upside.Candidates[1].P90);
    }

    [Fact]
    public void DS05_survival_to_the_next_pick_follows_the_opponents()
    {
        var (session, lineup, pool) = ScarceCenter();

        var board = Simulator.Simulate(session, lineup, pool, 9, RiskMode.Mean);

        board.NextUserPick.ShouldBe(4);
        var byId = board.Candidates.ToDictionary(candidate => candidate.PlayerId);
        byId[pool[1].PlayerId].SurvivalToNextPick.ShouldNotBeNull().ShouldBeLessThan(0.05m, "PG1 (ADP 3) goes at pick 2 or 3");
        byId[pool[4].PlayerId].SurvivalToNextPick.ShouldNotBeNull().ShouldBeGreaterThan(0.95m, "PG3 (ADP 5) is still there after PG1 and PG2 go");
    }

    private static (DraftSession Session, LineupOptimizer Lineup, DraftCandidate[] Pool) ScarceCenter()
    {
        var session = new DraftSession(Guid.NewGuid(), 2, 2, 1);
        var lineup = new LineupOptimizer([new RosterSlot(RosterSlotKind.C), new RosterSlot(RosterSlotKind.PG)], EightyTwoDays, LineupCadence.Daily);
        DraftCandidate[] pool =
        [
            Candidate("C1", 100m, 1m, ["C"], 1m),
            Candidate("PG1", 105m, 1m, ["PG"], 3m),
            Candidate("PG2", 104m, 1m, ["PG"], 4m),
            Candidate("C2", 20m, 1m, ["C"], 10m),
            Candidate("PG3", 103m, 1m, ["PG"], 5m),
        ];
        return (session, lineup, pool);
    }

    /// <summary>A player who plays all 82 games, with per-game points worth <paramref name="season"/> over the season.</summary>
    private static DraftCandidate Candidate(string name, decimal season, decimal perGameSd, IReadOnlyList<string> positions, decimal adp) => new(
        new PlayerId(Guid.NewGuid()),
        season,
        positions,
        adp,
        0m,
        0m,
        0m,
        new Dictionary<StatKey, decimal>(),
        AdpStandardDeviation: 0.1m,
        Distribution: new SeasonValueDistribution(season / 82m, perGameSd / 82m * 9m, new BetaBinomial(82, 100000m, 0.001m)),
        TeamId: Team);

    private static SeasonSchedule Schedule(int days) => new([.. Enumerable.Range(0, days).Select(day => new NbaGame(
        Guid.NewGuid(), 2026, new DateTimeOffset(2025, 10, 21, 23, 0, 0, TimeSpan.Zero).AddDays(day), Team, new NbaTeamId(Guid.NewGuid()), null, null, "scheduled",
        new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64))))]);
}
