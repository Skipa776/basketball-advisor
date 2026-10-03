using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Leagues;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Projections;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;
using FantasyBasketball.Domain.Statistics;

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
    NbaTeamId? TeamId = null);

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
        var bestValue = orderedValues[0].ProjectedSeasonValue;
        var valuePerPick = (bestValue - replacementValue)
            / Math.Max(1, replacementRank);
        // Null on the user's last pick of the draft: no later turn means nothing to price.
        var nextPick = session.NextUserPickAfterCurrent;
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
            // Positive when the player is still here past his ADP (a value), negative when
            // taking him now is a reach. Owner-approved sign correction 2026-09-23.
            var market = candidate.AverageDraftPosition is { } adp
                ? (session.CurrentPick - adp) * valuePerPick
                : 0m;
            var pAvailable = nextPick is { } pick
                ? EstimateAvailability(candidate.AverageDraftPosition, candidate.AdpStandardDeviation, pick)
                : null;
            // Losing the chance to take him is what urgency prices: value at risk times
            // the chance he is gone by the next turn. Null availability means zero urgency.
            var urgency = pAvailable is { } survival
                ? Math.Max(0m, varValue) * (1m - survival)
                : 0m;
            var evidence = CreateEvidence(
                varValue,
                scarcity,
                fit,
                market,
                candidate.AverageDraftPosition,
                candidate.InjuryRisk,
                candidate.RoleRisk,
                candidate.HasUnverifiedContext,
                pAvailable,
                urgency,
                nextPick);
            return calculator.Calculate(
                candidate.PlayerId,
                candidate.ProjectedSeasonValue,
                varValue,
                scarcity,
                fit,
                market,
                candidate.ContextAdjustment,
                candidate.InjuryRisk,
                candidate.RoleRisk,
                evidence,
                urgency,
                pAvailable);
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

    private static decimal? EstimateAvailability(
        decimal? adp,
        decimal? adpStandardDeviation,
        int nextPick)
    {
        if (adp is not { } average)
        {
            return null;
        }

        return 1m - NormalDistribution.Cdf(((decimal)nextPick - average) / AdpSigma(average, adpStandardDeviation));
    }

    /// <summary>
    /// The pick spread around an ADP, shared by the urgency estimate here and the simulated
    /// opponents in <c>DraftAssistService</c>.
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
        decimal market,
        decimal? adp,
        decimal injuryRisk,
        decimal roleRisk,
        bool hasUnverifiedContext,
        decimal? pAvailable,
        decimal urgency,
        int? nextPick)
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

        evidence.Add(new(
            EvidenceKind.Market,
            adp is null
                ? EvidencePolarity.Neutral
                : market >= 0m
                    ? EvidencePolarity.Supporting
                    : EvidencePolarity.Risk,
            adp is null ? "No ADP is available" : "Value relative to market ADP",
            adp is null ? null : market));
        if (pAvailable is { } survival)
        {
            evidence.Add(new(
                EvidenceKind.Market,
                survival < 0.5m
                    ? EvidencePolarity.Supporting
                    : EvidencePolarity.Neutral,
                $"About {(int)Math.Round(survival * 100m)}% chance he is still there at pick {nextPick}",
                urgency));
        }

        if (injuryRisk > 0m || roleRisk > 0m)
        {
            evidence.Add(new(
                EvidenceKind.Injury,
                EvidencePolarity.Risk,
                "Role or injury uncertainty reduces draft value",
                Math.Max(injuryRisk, roleRisk)));
        }

        if (hasUnverifiedContext)
        {
            evidence.Add(new RecommendationEvidence(EvidenceKind.Context, EvidencePolarity.Risk,
                "Projection includes unverified context; review the underlying estimates", null));
        }

        return evidence;
    }
}
