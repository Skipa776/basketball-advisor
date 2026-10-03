using System.Diagnostics;
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

/// <summary>
/// The draft-room latency budget (draft_simulation): a recommendation at the very first pick of a
/// 12-team, 13-round draft — the most picks left to simulate — with the production K = 15 and
/// N = 500, on a 30-team, 165-day schedule and a 300-player pool. Fails if p95 exceeds 2 s.
/// The gate runs it in Release without coverage instrumentation (scripts/gate.sh).
/// </summary>
[Trait("Category", "Performance")]
public sealed class DraftSimulatorPerfTests
{
    private static readonly RosterSlot[] Slots =
    [
        new(RosterSlotKind.PG), new(RosterSlotKind.SG), new(RosterSlotKind.SF), new(RosterSlotKind.PF), new(RosterSlotKind.C),
        new(RosterSlotKind.G), new(RosterSlotKind.F), new(RosterSlotKind.UTIL), new(RosterSlotKind.UTIL), new(RosterSlotKind.UTIL),
        new(RosterSlotKind.BENCH), new(RosterSlotKind.BENCH), new(RosterSlotKind.BENCH),
    ];

    [Fact]
    public void DS06_first_pick_of_a_twelve_team_draft_simulates_inside_two_seconds_at_p95()
    {
        var random = new Random(7);
        var teams = Enumerable.Range(0, 30).Select(_ => new NbaTeamId(Guid.NewGuid())).ToArray();
        var games = new List<NbaGame>();
        for (var day = 0; day < 165; day++)
        {
            var shuffled = teams.OrderBy(_ => random.Next()).Take(2 * random.Next(3, 8)).ToArray();
            for (var i = 0; i < shuffled.Length; i += 2)
            {
                games.Add(new NbaGame(Guid.NewGuid(), 2027, new DateTimeOffset(2026, 10, 20, 23, 0, 0, TimeSpan.Zero).AddDays(day),
                    shuffled[i], shuffled[i + 1], null, null, "scheduled",
                    new DataProvenance(DataSourceName.Manual, null, DateTimeOffset.UnixEpoch, null, "manual-v1", 1m, new string('a', 64))));
            }
        }

        var lineup = new LineupOptimizer(Slots, new SeasonSchedule(games), LineupCadence.Daily);
        string[][] positions = [["PG"], ["SG"], ["SF"], ["PF"], ["C"], ["PG", "SG"], ["SF", "PF"], ["PF", "C"]];
        var pool = Enumerable.Range(1, 300).Select(rank =>
        {
            var perGame = 55m - (0.15m * rank);
            return new DraftCandidate(new PlayerId(Guid.NewGuid()), perGame * 65m, positions[rank % positions.Length], rank, 0m, 0m, 0m,
                new Dictionary<StatKey, decimal>(), Distribution: new SeasonValueDistribution(perGame, 0.15m * perGame, new BetaBinomial(82, 6m, 1.5m)),
                TeamId: teams[rank % teams.Length]);
        }).ToArray();
        var simulator = new DraftSimulator(new SimulationOptions());
        var session = new DraftSession(Guid.NewGuid(), 12, 13, 1);
        simulator.Simulate(session, lineup, pool, 1, RiskMode.Mean); // warm-up: JIT and tiering

        var timings = Enumerable.Range(0, 10).Select(run =>
        {
            var watch = Stopwatch.StartNew();
            simulator.Simulate(session, lineup, pool, run, RiskMode.Mean);
            return watch.Elapsed.TotalMilliseconds;
        }).Order().ToArray();

        var p95 = timings[(int)Math.Ceiling(0.95 * timings.Length) - 1];
        TestContext.Current.SendDiagnosticMessage($"DS06 timings ms: {string.Join(", ", timings.Select(t => t.ToString("F0")))}");
        p95.ShouldBeLessThan(2000, $"p95 {p95:F0} ms");
    }
}
