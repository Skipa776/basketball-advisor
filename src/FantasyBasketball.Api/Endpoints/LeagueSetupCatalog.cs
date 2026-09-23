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
            Rules = LeagueCatalog.EspnDefaultPointsRules
                .Select(rule => new ScoringRuleRequest(rule.Stat.ToString(), rule.PointsPerUnit))
                .ToArray(),
        },
    };
}
