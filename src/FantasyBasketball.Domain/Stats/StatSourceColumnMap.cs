using System.Collections.ObjectModel;

namespace FantasyBasketball.Domain.Stats;

public static class StatSourceColumnMap
{
    public static IReadOnlyDictionary<string, StatKey> BoxScoreCounting { get; } =
        new ReadOnlyDictionary<string, StatKey>(new Dictionary<string, StatKey>(StringComparer.Ordinal)
        {
            ["mp"] = StatKey.MIN,
            ["pts"] = StatKey.PTS,
            ["orb"] = StatKey.OREB,
            ["drb"] = StatKey.DREB,
            ["trb"] = StatKey.REB,
            ["ast"] = StatKey.AST,
            ["stl"] = StatKey.STL,
            ["blk"] = StatKey.BLK,
            ["tov"] = StatKey.TOV,
            ["fg"] = StatKey.FGM,
            ["fga"] = StatKey.FGA,
            ["fg3"] = StatKey.FG3M,
            ["fg3a"] = StatKey.FG3A,
            ["ft"] = StatKey.FTM,
            ["fta"] = StatKey.FTA,
            ["pf"] = StatKey.PF,
        });

    public static IReadOnlyDictionary<string, StatKey> SeasonTableCounting { get; } =
        new ReadOnlyDictionary<string, StatKey>(
            new Dictionary<string, StatKey>(StringComparer.Ordinal)
            {
                ["MP"] = StatKey.MIN,
                [nameof(StatKey.PTS)] = StatKey.PTS,
                ["ORB"] = StatKey.OREB,
                ["DRB"] = StatKey.DREB,
                ["TRB"] = StatKey.REB,
                [nameof(StatKey.AST)] = StatKey.AST,
                [nameof(StatKey.STL)] = StatKey.STL,
                [nameof(StatKey.BLK)] = StatKey.BLK,
                [nameof(StatKey.TOV)] = StatKey.TOV,
                ["FG"] = StatKey.FGM,
                ["FGA"] = StatKey.FGA,
                ["3P"] = StatKey.FG3M,
                ["3PA"] = StatKey.FG3A,
                ["FT"] = StatKey.FTM,
                ["FTA"] = StatKey.FTA,
                ["PF"] = StatKey.PF,
            });
}
