using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Leagues;

public sealed class FantasyLeagueTests
{
    [Fact]
    public void Points_league_requires_scoring_rules()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateLeague(
            LeagueType.Points,
            [],
            []));

        exception.Message.ShouldContain(nameof(FantasyLeague.ScoringRules));
    }

    [Fact]
    public void Category_league_requires_categories()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateLeague(
            LeagueType.Categories,
            [],
            []));

        exception.Message.ShouldContain(nameof(FantasyLeague.Categories));
    }

    [Fact]
    public void Duplicate_scoring_stat_is_rejected()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateLeague(
            LeagueType.Points,
            [
                new ScoringRule(StatKey.PTS, 1m),
                new ScoringRule(StatKey.PTS, 2m),
            ],
            []));

        exception.Message.ShouldContain(nameof(FantasyLeague.ScoringRules));
        exception.Message.ShouldContain(nameof(StatKey.PTS));
    }

    [Fact]
    public void Seed_league_has_the_canonical_configuration()
    {
        var league = LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());

        league.Name.ShouldBe("Seed Points League");
        league.Type.ShouldBe(LeagueType.Points);
        league.TeamCount.ShouldBe(10);
        league.Cadence.ShouldBe(LineupCadence.Daily);
        league.ScoringRules.Count.ShouldBe(6);
        league.RosterSlots.Count.ShouldBe(13);
    }

    [Theory]
    [InlineData(RosterSlotKind.G, "PG", true)]
    [InlineData(RosterSlotKind.G, "SF", false)]
    [InlineData(RosterSlotKind.F, "PF", true)]
    [InlineData(RosterSlotKind.F, "SG", false)]
    [InlineData(RosterSlotKind.UTIL, "C", true)]
    [InlineData(RosterSlotKind.BENCH, "PG", true)]
    [InlineData(RosterSlotKind.IR, "SF", true)]
    public void Roster_slot_eligibility_is_owned_by_the_slot(
        RosterSlotKind kind,
        string position,
        bool expected)
    {
        new RosterSlot(kind).Accepts(position).ShouldBe(expected);
    }

    [Fact]
    public void Weekly_acquisitions_default_to_seven_and_stay_in_range()
    {
        CreateLeague(LeagueType.Points, [new ScoringRule(StatKey.PTS, 1m)], []).WeeklyAcquisitionLimit.ShouldBe(7);
        foreach (var invalid in new[] { 0, 100 })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new FantasyLeague(Guid.NewGuid(), "L", LeagueType.Points, 10,
                [new ScoringRule(StatKey.PTS, 1m)], [], [new RosterSlot(RosterSlotKind.UTIL)], LineupCadence.Daily, invalid));
        }
    }

    private static FantasyLeague CreateLeague(
        LeagueType type,
        IReadOnlyList<ScoringRule> rules,
        IReadOnlyList<StatKey> categories) =>
        new(
            Guid.NewGuid(),
            "Test League",
            type,
            10,
            rules,
            categories,
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);
}
