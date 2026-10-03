using FantasyBasketball.Application.Backtest;
using FantasyBasketball.Domain.Draft;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Application.Tests.Backtest;

public sealed class DraftBenchmarkTests
{
    private static readonly string[] Positions = ["PG", "SG", "SF", "PF", "C"];
    private static readonly FantasyLeague League = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());

    [Fact]
    public void B05_same_seeds_give_identical_drafts_and_objective()
    {
        var (candidates, actual) = Pool();
        var benchmark = new DraftBenchmark(Board());

        var first = benchmark.Run(League, candidates, actual, drafts: 10);
        var second = benchmark.Run(League, candidates, actual, drafts: 10);

        second.Differences.SequenceEqual(first.Differences).ShouldBeTrue();
        second.MeanBoard.ShouldBe(first.MeanBoard);
    }

    [Fact]
    public void B06_the_objective_scores_actual_production_not_projections()
    {
        var (candidates, actual) = Pool();
        var roster = candidates.Take(13).ToArray();
        var reprojected = roster.Select(candidate => candidate with { ProjectedSeasonValue = 1m }).ToArray();

        DraftBenchmark.LineupValue(reprojected, League, actual)
            .ShouldBe(DraftBenchmark.LineupValue(roster, League, actual));
    }

    [Fact]
    public void DB01_board_beats_an_adp_bot_when_adp_misplaces_value()
    {
        // Projections equal actual value; every seventh player's ADP is 40 picks too late,
        // so the board takes those values early while the ADP bot waits like the room does.
        var (candidates, actual) = Pool();

        var result = new DraftBenchmark(Board()).Run(League, candidates, actual, drafts: 20);

        result.MeanDifference.ShouldBeGreaterThan(0m);
        result.CiLow.ShouldBeGreaterThan(0m);
        result.Differences.Count.ShouldBe(20);
    }

    [Fact]
    public void DB02_lineup_value_fills_starting_slots_by_position_and_ignores_the_bench()
    {
        var actual = new Dictionary<PlayerId, decimal>();
        var centers = Enumerable.Range(0, 5).Select(index => Candidate(index, "C", 100m - index, actual)).ToArray();

        // Seed slots: PG SG SF PF C G F UTIL UTIL. Five centers fill C and both UTIL;
        // the other two sit on the bench and score nothing.
        DraftBenchmark.LineupValue(centers, League, actual).ShouldBe(100m + 99m + 98m);
    }

    [Fact]
    public void DB03_drafters_are_paired_on_the_same_seeded_drafts()
    {
        var (candidates, actual) = Pool();
        var benchmark = new DraftBenchmark(Board());

        var result = benchmark.Compare(League, candidates, actual,
            [("ADP bot", DraftBenchmark.AdpDrafter), ("ADP twin", DraftBenchmark.AdpDrafter), ("Board", benchmark.BoardDrafter)], drafts: 12);

        var twin = result.Comparisons.Single(comparison => comparison.Drafter == "ADP twin");
        twin.MeanDifference.ShouldBe(0m, "an identical drafter on identical drafts differs by exactly zero");
        twin.CiLow.ShouldBe(0m);
        result.Comparisons.Count.ShouldBe(3);
        result.MeanValue.Keys.ShouldBe(["ADP bot", "ADP twin", "Board"]);
        result.Comparisons.Single(comparison => comparison.Drafter == "Board" && comparison.Against == "ADP bot").MeanDifference
            .ShouldBe(new DraftBenchmark(Board()).Run(League, candidates, actual, drafts: 12).MeanDifference);
    }

    [Fact]
    public void DB04_opponents_draft_by_adp_whatever_order_the_pool_arrives_in()
    {
        // Regression: SimulatedOpponent once jittered the first 12 players of its input, so an
        // unsorted pool had the room draft arbitrary players and every user roster came out the same.
        var (candidates, actual) = Pool();
        var shuffled = candidates.OrderBy(candidate => candidate.PlayerId.Value).ToArray();

        var sorted = new DraftBenchmark(Board()).Run(League, candidates, actual, drafts: 10);
        var unsorted = new DraftBenchmark(Board()).Run(League, shuffled, actual, drafts: 10);

        unsorted.Differences.ShouldBe(sorted.Differences);
        unsorted.MeanAdpBot.ShouldBe(sorted.MeanAdpBot);
    }

    private static DraftBoard Board() => new(new DraftValueCalculator(new DraftWeightOptions()));

    private static (IReadOnlyList<DraftCandidate> Candidates, Dictionary<PlayerId, decimal> Actual) Pool()
    {
        var actual = new Dictionary<PlayerId, decimal>();
        var candidates = Enumerable.Range(0, 160)
            .Select(index => Candidate(index, Positions[index % Positions.Length], 2000m - (10m * index), actual))
            .Select((candidate, index) => index % 7 == 3
                ? candidate with { AverageDraftPosition = candidate.AverageDraftPosition + 40m }
                : candidate)
            .ToArray();
        return (candidates, actual);
    }

    private static DraftCandidate Candidate(
        int index,
        string position,
        decimal value,
        Dictionary<PlayerId, decimal> actual)
    {
        var id = new PlayerId(new Guid(index + 1, 0, 0, new byte[8]));
        actual[id] = value;
        return new DraftCandidate(
            id,
            value,
            [position],
            index + 1m,
            0m,
            0m,
            0m,
            new Dictionary<StatKey, decimal>());
    }
}
