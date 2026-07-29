using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Scoring;

public sealed class ScoringEngineTests
{
    private readonly FantasyLeague seedLeague =
        LeagueCatalog.CreateSeedPointsLeague(Guid.NewGuid());

    [Fact]
    public void Seed_league_golden_scores_exactly()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 17.25m,
            [StatKey.REB] = 5.85m,
            [StatKey.AST] = 4.275m,
            [StatKey.STL] = 1.125m,
            [StatKey.BLK] = 0.6m,
            [StatKey.TOV] = 2.775m,
            [StatKey.MIN] = 30m,
            [StatKey.FGM] = 6.5m,
        });

        new PointsScoringEngine().Score(line, seedLeague).ShouldBe(33.0825m);
    }

    [Fact]
    public void Scoring_is_pure_and_deterministic()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 25m,
            [StatKey.AST] = 8m,
        });
        var engine = new PointsScoringEngine();

        var first = engine.Score(line, seedLeague);
        var second = engine.Score(line, seedLeague);

        second.ShouldBe(first);
    }

    [Fact]
    public void Unscored_stats_contribute_zero()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.MIN] = 40m,
            [StatKey.FGM] = 15m,
            [StatKey.PF] = 5m,
        });

        new PointsScoringEngine().Score(line, seedLeague).ShouldBe(0m);
    }

    [Fact]
    public void Negative_score_is_not_clamped()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 1m,
            [StatKey.TOV] = 2m,
        });

        new PointsScoringEngine().Score(line, seedLeague).ShouldBe(-1m);
    }

    [Fact]
    public void Aggregate_recomputes_ratio_from_combined_attempts()
    {
        var first = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 1m,
            [StatKey.FGA] = 2m,
        });
        var second = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 9m,
            [StatKey.FGA] = 10m,
        });

        var aggregate = StatLine.Aggregate([first, second]);

        aggregate[StatKey.FG_PCT].ShouldBe(10m / 12m);
        aggregate[StatKey.FG_PCT].ShouldNotBe(0.7m);
    }

    [Fact]
    public void Category_engine_returns_only_configured_category_totals()
    {
        var league = new FantasyLeague(
            Guid.NewGuid(),
            "Nine Category",
            LeagueType.Categories,
            12,
            [],
            LeagueCatalog.StandardCategories,
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Weekly);
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 8m,
            [StatKey.FGA] = 16m,
            [StatKey.FTM] = 6m,
            [StatKey.FTA] = 8m,
            [StatKey.FG3M] = 4m,
            [StatKey.FG3A] = 10m,
            [StatKey.PTS] = 24m,
            [StatKey.REB] = 7m,
            [StatKey.AST] = 6m,
            [StatKey.STL] = 2m,
            [StatKey.BLK] = 1m,
            [StatKey.TOV] = 3m,
            [StatKey.MIN] = 35m,
        });

        var result = new CategoryScoringEngine().ScoreCategories(line, league);

        result.Count.ShouldBe(9);
        result[StatKey.FG_PCT].ShouldBe(0.5m);
        result[StatKey.FT_PCT].ShouldBe(0.75m);
        result[StatKey.FG3M].ShouldBe(4m);
        result.ShouldNotContainKey(StatKey.MIN);
    }

    [Fact]
    public void Engines_reject_the_wrong_league_result_shape()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>());
        var categoryLeague = new FantasyLeague(
            Guid.NewGuid(),
            "Categories",
            LeagueType.Categories,
            10,
            [],
            [StatKey.PTS],
            [new RosterSlot(RosterSlotKind.UTIL)],
            LineupCadence.Daily);

        Should.Throw<ArgumentException>(() =>
            new PointsScoringEngine().Score(line, categoryLeague));
        Should.Throw<NotSupportedException>(() =>
            new PointsScoringEngine().ScoreCategories(line, seedLeague));
        Should.Throw<ArgumentException>(() =>
            new CategoryScoringEngine().ScoreCategories(line, seedLeague));
        Should.Throw<NotSupportedException>(() =>
            new CategoryScoringEngine().Score(line, categoryLeague));
    }
}
