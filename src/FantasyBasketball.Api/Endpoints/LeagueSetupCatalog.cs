using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Api.Endpoints;

/// <summary>Explicit setup choices, never scoring-engine fallback values.</summary>
public static class LeagueSetupCatalog
{
    public static object Create() => new
    {
        SuggestedTeamCount = 7,
        ConfidenceLevels = Enum.GetValues<Confidence>().Select(value => new { Name = value.ToString(), Value = (int)value }),
        EvidencePolarities = Enum.GetValues<EvidencePolarity>().Select(value => new { Name = value.ToString(), Value = (int)value }),
        Stats = Enum.GetValues<StatKey>().Select(value => new { Name = value.ToString(), Value = (int)value }),
        RosterSlots = Enum.GetValues<RosterSlotKind>().Select(value => value.ToString()),
        PointsProfile = new
        {
            Id = "espn-default-points",
            Name = "ESPN default points",
            Rules = new ScoringRuleRequest[]
            {
                new(nameof(StatKey.PTS), 1m),
                new(nameof(StatKey.FG3M), 1m),
                new(nameof(StatKey.FGM), 2m),
                new(nameof(StatKey.FGA), -1m),
                new(nameof(StatKey.FTM), 1m),
                new(nameof(StatKey.FTA), -1m),
                new(nameof(StatKey.REB), 1m),
                new(nameof(StatKey.AST), 2m),
                new(nameof(StatKey.STL), 4m),
                new(nameof(StatKey.BLK), 4m),
                new(nameof(StatKey.TOV), -2m),
            },
        },
    };
}
