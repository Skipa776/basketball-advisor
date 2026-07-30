using FantasyBasketball.Domain.Stats;
using Shouldly;

namespace FantasyBasketball.Domain.Tests.Stats;

public sealed class StatVocabularyTests
{
    [Fact]
    public void StatKey_members_match_the_canonical_catalog()
    {
        Enum.GetNames<StatKey>().ShouldBe(
        [
            "MIN",
            "PTS",
            "OREB",
            "DREB",
            "REB",
            "AST",
            "STL",
            "BLK",
            "TOV",
            "FGM",
            "FGA",
            "FG3M",
            "FG3A",
            "FTM",
            "FTA",
            "PF",
            "FG_PCT",
            "FT_PCT",
            "FG3_PCT",
        ]);
    }

    [Fact]
    public void Season_source_columns_match_the_canonical_counting_map()
    {
        StatSourceColumnMap.SeasonTableCounting.ShouldBe(
            new Dictionary<string, StatKey>(StringComparer.Ordinal)
            {
                ["MP"] = StatKey.MIN,
                ["PTS"] = StatKey.PTS,
                ["ORB"] = StatKey.OREB,
                ["DRB"] = StatKey.DREB,
                ["TRB"] = StatKey.REB,
                ["AST"] = StatKey.AST,
                ["STL"] = StatKey.STL,
                ["BLK"] = StatKey.BLK,
                ["TOV"] = StatKey.TOV,
                ["FG"] = StatKey.FGM,
                ["FGA"] = StatKey.FGA,
                ["3P"] = StatKey.FG3M,
                ["3PA"] = StatKey.FG3A,
                ["FT"] = StatKey.FTM,
                ["FTA"] = StatKey.FTA,
                ["PF"] = StatKey.PF,
            });
    }

    [Fact]
    public void Missing_stat_reads_as_zero()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.PTS] = 20m,
        });

        line[StatKey.AST].ShouldBe(0m);
    }

    [Fact]
    public void Ratio_stats_are_derived_from_counting_stats()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 9m,
            [StatKey.FGA] = 20m,
            [StatKey.FTM] = 4m,
            [StatKey.FTA] = 5m,
            [StatKey.FG3M] = 3m,
            [StatKey.FG3A] = 8m,
        });

        line[StatKey.FG_PCT].ShouldBe(0.45m);
        line[StatKey.FT_PCT].ShouldBe(0.8m);
        line[StatKey.FG3_PCT].ShouldBe(0.375m);
    }

    [Fact]
    public void Ratio_with_zero_denominator_reads_as_zero()
    {
        var line = new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FGM] = 0m,
            [StatKey.FGA] = 0m,
        });

        line[StatKey.FG_PCT].ShouldBe(0m);
    }

    [Fact]
    public void Ratio_values_cannot_be_supplied_as_source_values()
    {
        Should.Throw<ArgumentException>(() => new StatLine(new Dictionary<StatKey, decimal>
        {
            [StatKey.FG_PCT] = 0.5m,
        })).Message.ShouldContain("ratio");
    }
}
