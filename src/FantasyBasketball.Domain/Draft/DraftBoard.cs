using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Draft;

public sealed record DraftCandidate(
    PlayerId PlayerId,
    decimal ProjectedSeasonValue,
    IReadOnlyList<string> Positions,
    decimal? AverageDraftPosition,
    decimal ContextAdjustment,
    decimal InjuryRisk,
    decimal RoleRisk,
    IReadOnlyDictionary<StatKey, decimal> CategoryTotals,
    bool HasUnverifiedContext = false,
    decimal? AdpStandardDeviation = null,
    SeasonValueDistribution? Distribution = null,
    NbaTeamId? TeamId = null,
    bool SatOutLastSeason = false);

public sealed record DraftBoardResult(
    IReadOnlyList<DraftValue> Rankings,
    string? Banner);

public sealed class DraftBoard(DraftValueCalculator calculator)
{
    public DraftBoardResult Rank(
        DraftSession session,
        FantasyLeague league,
        IReadOnlyList<DraftCandidate> candidates,
        IReadOnlyList<PlayerId> userRoster)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(userRoster);
        var drafted = session.Picks.Select(pick => pick.PlayerId).ToHashSet();
        var available = candidates
            .Where(candidate => !drafted.Contains(candidate.PlayerId))
            .ToArray();
        if (available.Length == 0)
        {
            return new DraftBoardResult([], null);
        }

        var startersPerTeam = league.RosterSlots.Count(slot =>
            slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR);
        var replacementRank = league.TeamCount * startersPerTeam;
        var orderedValues = available
            .OrderByDescending(candidate => candidate.ProjectedSeasonValue)
            .ToArray();
        var replacementValue = orderedValues[
            Math.Min(replacementRank, orderedValues.Length) - 1]
            .ProjectedSeasonValue;
        var values = available.Select(candidate =>
        {
            var varValue = candidate.ProjectedSeasonValue - replacementValue;
            var scarcity = CalculateScarcity(
                candidate,
                available,
                session.PicksUntilNextTurn,
                replacementValue);
            var fit = CalculateRosterFit(
                candidate,
                candidates,
                userRoster,
                league,
                replacementValue);
            var evidence = CreateEvidence(
                varValue,
                scarcity,
                fit,
                candidate.InjuryRisk,
                candidate.RoleRisk,
                candidate.HasUnverifiedContext,
                candidate.SatOutLastSeason);
            return calculator.Calculate(
                candidate.PlayerId,
                candidate.ProjectedSeasonValue,
                varValue,
                scarcity,
                fit,
                candidate.ContextAdjustment,
                candidate.InjuryRisk,
                candidate.RoleRisk,
                evidence);
        });

        var categoryLeague = league.Type == LeagueType.Categories;
        var sorted = categoryLeague
            ? values.OrderByDescending(value =>
                league.Categories.Sum(category =>
                    candidates.Single(candidate => candidate.PlayerId == value.PlayerId)
                        .CategoryTotals.GetValueOrDefault(category)))
            : values.OrderByDescending(value => value.Total);
        return new DraftBoardResult(
            new ReadOnlyCollection<DraftValue>(sorted
                .ThenBy(value => value.PlayerId.Value)
                .ToArray()),
            categoryLeague
                ? "Category league fallback: ranked by projected category totals."
                : null);
    }

    private static decimal CalculateScarcity(
        DraftCandidate candidate,
        IReadOnlyList<DraftCandidate> available,
        int picksUntilNextTurn,
        decimal replacementValue) =>
        candidate.Positions
            .Select(position =>
            {
                var atPosition = available
                    .Where(other => other.Positions.Contains(position))
                    .OrderByDescending(other => other.ProjectedSeasonValue)
                    .ToArray();
                var future = atPosition.Length >= picksUntilNextTurn
                    ? atPosition[picksUntilNextTurn - 1].ProjectedSeasonValue
                    : replacementValue;
                return Math.Max(0m, atPosition[0].ProjectedSeasonValue - future);
            })
            .DefaultIfEmpty(0m)
            .Max();

    private static decimal CalculateRosterFit(
        DraftCandidate candidate,
        IReadOnlyList<DraftCandidate> allCandidates,
        IReadOnlyList<PlayerId> userRoster,
        FantasyLeague league,
        decimal replacementValue)
    {
        var eligibleSlots = league.RosterSlots.Where(slot =>
            slot.Kind is not RosterSlotKind.BENCH and not RosterSlotKind.IR
            && candidate.Positions.Any(slot.Accepts)).ToArray();
        if (eligibleSlots.Length == 0)
        {
            return 0m;
        }

        var eligibleRoster = allCandidates.Where(other =>
            userRoster.Contains(other.PlayerId)
            && other.Positions.Any(position =>
                eligibleSlots.Any(slot => slot.Accepts(position)))).ToArray();
        return eligibleRoster.Length < eligibleSlots.Length
            ? 0m
            : replacementValue
                - eligibleRoster.Min(player => player.ProjectedSeasonValue);
    }

    /// <summary>
    /// The pick spread around an ADP, shared by the simulated opponents (<see cref="SimulatedOpponent"/>
    /// and <see cref="DraftSimulator"/>).
    /// </summary>
    public static decimal AdpSigma(decimal adp, decimal? standardDeviation)
    {
        // ponytail: heuristic spread — a published ADP standard deviation when the source
        // gives one, otherwise 20% of ADP with a floor of 6 picks (calibrated 2026-09-26 on a
        // real 150-pick Sleeper draft: 54–73% of picks within one sigma per round band). The
        // upgrade is deriving sigma from real Sleeper/platform pick distributions instead.
        return standardDeviation is > 0m
            ? standardDeviation.Value
            : Math.Max(6m, 0.2m * adp);
    }

    private static IReadOnlyList<RecommendationEvidence> CreateEvidence(
        decimal varValue,
        decimal scarcity,
        decimal fit,
        decimal injuryRisk,
        decimal roleRisk,
        bool hasUnverifiedContext,
        bool satOutLastSeason)
    {
        var evidence = new List<RecommendationEvidence>
        {
            new(
                EvidenceKind.Opportunity,
                varValue >= 0m
                    ? EvidencePolarity.Supporting
                    : EvidencePolarity.Risk,
                "Value above current replacement level",
                varValue),
        };
        if (scarcity > 0m)
        {
            evidence.Add(new(
                EvidenceKind.Scarcity,
                EvidencePolarity.Supporting,
                "Position loses value before the next turn",
                scarcity));
        }

        if (fit < 0m)
        {
            evidence.Add(new(
                EvidenceKind.RosterFit,
                EvidencePolarity.Risk,
                "Eligible starting slots are already occupied",
                fit));
        }

        if (injuryRisk > 0m || roleRisk > 0m)
        {
            evidence.Add(new(
                EvidenceKind.Injury,
                EvidencePolarity.Risk,
                "Role or injury uncertainty reduces draft value",
                Math.Max(injuryRisk, roleRisk)));
        }

        if (satOutLastSeason)
        {
            evidence.Add(new RecommendationEvidence(EvidenceKind.Injury, EvidencePolarity.Risk,
                "Sat out last season; projected from the seasons before it", null));
        }

        if (hasUnverifiedContext)
        {
            evidence.Add(new RecommendationEvidence(EvidenceKind.Context, EvidencePolarity.Risk,
                "Projection includes unverified context; review the underlying estimates", null));
        }

        return evidence;
    }
}
