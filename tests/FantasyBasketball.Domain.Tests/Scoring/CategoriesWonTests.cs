using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Scoring;
using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Scoring;

public sealed class CategoriesWonTests
{
    private static StatLine Line(
        decimal pts = 0, decimal reb = 0, decimal ast = 0, decimal stl = 0, decimal blk = 0,
        decimal fg3m = 0, decimal tov = 0, decimal fgm = 0, decimal fga = 0, decimal ftm = 0, decimal fta = 0) =>
        new(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = pts,
            [StatKey.REB] = reb,
            [StatKey.AST] = ast,
            [StatKey.STL] = stl,
            [StatKey.BLK] = blk,
            [StatKey.FG3M] = fg3m,
            [StatKey.TOV] = tov,
            [StatKey.FGM] = fgm,
            [StatKey.FGA] = fga,
            [StatKey.FTM] = ftm,
            [StatKey.FTA] = fta,
        });

    [Fact]
    public void A_line_that_beats_the_pool_everywhere_wins_all_nine()
    {
        var star = Line(40, 12, 10, 3, 2, 5, 1, 15, 20, 5, 5);
        var bench = Line(4, 2, 1, 0, 0, 0, 3, 2, 8, 1, 2);
        CategoriesWon.Count(star, [star, bench]).ShouldBe(9);
        CategoriesWon.Count(bench, [star, bench]).ShouldBe(0);
    }

    [Fact]
    public void Ties_win_nothing_and_fewer_turnovers_win()
    {
        var same = Line(10, 5, 5, 1, 1, 1, 2, 4, 8, 2, 2);
        CategoriesWon.Count(same, [same, same]).ShouldBe(0);

        var careful = same with { };
        var sloppy = Line(10, 5, 5, 1, 1, 1, 6, 4, 8, 2, 2);
        CategoriesWon.Count(careful, [careful, sloppy]).ShouldBe(1);
    }

    [Fact]
    public void Percentages_use_pooled_makes_and_zero_attempts_never_win()
    {
        // Pool FG%: (1 + 9) / (1 + 20) = 47.6%. One-for-one beats it; 9/20 (45%) does not,
        // even though the per-player average of 100% and 45% would say otherwise.
        var efficient = Line(fgm: 1, fga: 1);
        var volume = Line(fgm: 9, fga: 20);
        CategoriesWon.Count(efficient, [efficient, volume]).ShouldBe(1);
        CategoriesWon.Count(volume, [efficient, volume]).ShouldBe(0);

        var noShots = Line(reb: 1);
        var shooter = Line(fgm: 1, fga: 4, ftm: 1, fta: 4);
        CategoriesWon.Count(noShots, [noShots, shooter]).ShouldBe(1, "only rebounds; 0 FGA/FTA never wins FG% or FT%");
    }

    [Fact]
    public void An_empty_pool_is_rejected()
    {
        Should.Throw<ArgumentException>(() => CategoriesWon.Count(Line(), []));
        LeagueCatalog.StandardCategories.Count.ShouldBe(9);
    }
}
