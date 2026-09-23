using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Leagues;

public static class LeagueCatalog
{
    public static IReadOnlyList<StatKey> StandardCategories { get; } =
        new ReadOnlyCollection<StatKey>(
        [
            StatKey.FG_PCT,
            StatKey.FT_PCT,
            StatKey.FG3M,
            StatKey.PTS,
            StatKey.REB,
            StatKey.AST,
            StatKey.STL,
            StatKey.BLK,
            StatKey.TOV,
        ]);

    /// <summary>ESPN's default points scoring. The setup catalog and the public landing both use it.</summary>
    public static IReadOnlyList<ScoringRule> EspnDefaultPointsRules { get; } = Rules(
        new(StatKey.PTS, 1m),
        new(StatKey.FG3M, 1m),
        new(StatKey.FGM, 2m),
        new(StatKey.FGA, -1m),
        new(StatKey.FTM, 1m),
        new(StatKey.FTA, -1m),
        new(StatKey.REB, 1m),
        new(StatKey.AST, 2m),
        new(StatKey.STL, 4m),
        new(StatKey.BLK, 4m),
        new(StatKey.TOV, -2m));

    /// <summary>An unsaved league used to score public reference data under ESPN defaults.</summary>
    public static FantasyLeague CreateEspnDefaultPointsLeague() =>
        new(
            Guid.Parse("00000000-0000-0000-0000-00000000e5b1"),
            "ESPN default points",
            LeagueType.Points,
            10,
            EspnDefaultPointsRules,
            [],
            Slots(RosterSlotKind.UTIL),
            LineupCadence.Daily);

    public static FantasyLeague CreateSeedPointsLeague(Guid id) =>
        new(
            id,
            "Seed Points League",
            LeagueType.Points,
            10,
            Rules(
                new(StatKey.PTS, 1.0m),
                new(StatKey.REB, 1.2m),
                new(StatKey.AST, 1.5m),
                new(StatKey.STL, 3.0m),
                new(StatKey.BLK, 3.0m),
                new(StatKey.TOV, -1.0m)),
            [],
            Slots(
                RosterSlotKind.PG,
                RosterSlotKind.SG,
                RosterSlotKind.SF,
                RosterSlotKind.PF,
                RosterSlotKind.C,
                RosterSlotKind.G,
                RosterSlotKind.F,
                RosterSlotKind.UTIL,
                RosterSlotKind.UTIL,
                RosterSlotKind.BENCH,
                RosterSlotKind.BENCH,
                RosterSlotKind.BENCH,
                RosterSlotKind.IR),
            LineupCadence.Daily);

    private static IReadOnlyList<ScoringRule> Rules(params ScoringRule[] rules) =>
        new ReadOnlyCollection<ScoringRule>(rules);

    private static IReadOnlyList<RosterSlot> Slots(params RosterSlotKind[] kinds) =>
        new ReadOnlyCollection<RosterSlot>(kinds.Select(kind => new RosterSlot(kind)).ToArray());
}
