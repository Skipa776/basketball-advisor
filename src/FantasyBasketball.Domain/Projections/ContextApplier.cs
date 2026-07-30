using FantasyBasketball.Domain.Context;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

public sealed class ContextApplier(ConfidenceCalculator confidenceCalculator)
{
    private static readonly IReadOnlySet<StatKey> UsageStats = new HashSet<StatKey>
    {
        StatKey.PTS,
        StatKey.FGM,
        StatKey.FGA,
        StatKey.FG3M,
        StatKey.FG3A,
        StatKey.FTM,
        StatKey.FTA,
        StatKey.TOV,
    };

    private static readonly IReadOnlySet<StatKey> ReboundStats = new HashSet<StatKey>
    {
        StatKey.REB,
        StatKey.OREB,
        StatKey.DREB,
    };

    public AdjustedProjection Apply(
        Guid adjustedProjectionId,
        BaselineProjection baseline,
        IReadOnlyList<ContextEvent> events,
        IReadOnlyList<PlayerContextImpact> impacts,
        DateTimeOffset computedAt)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(impacts);
        if (computedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("ComputedAt must be UTC.", nameof(computedAt));
        }

        var applicableEvents = events
            .Where(contextEvent =>
                contextEvent.AffectedPlayerIds.Contains(baseline.PlayerId)
                && contextEvent.EffectiveFrom <= computedAt
                && (contextEvent.ExpectedExpiration is null
                    || contextEvent.ExpectedExpiration > computedAt)
                && contextEvent.Verification != VerificationState.Rejected)
            .OrderBy(contextEvent => contextEvent.Id)
            .ToArray();
        var eventIds = applicableEvents.Select(value => value.Id).ToHashSet();
        var applicableImpacts = impacts
            .Where(impact =>
                impact.PlayerId == baseline.PlayerId
                && eventIds.Contains(impact.ContextEventId))
            .ToArray();

        var minutes = Math.Clamp(
            baseline.ProjectedMinutesPerGame
                + applicableImpacts.Sum(impact => impact.MinutesDelta),
            0m,
            42m);
        var usage = AggregateMultiplier(applicableImpacts.Select(value => value.UsageDelta));
        var shotVolume = AggregateMultiplier(
            applicableImpacts.Select(value => value.ShotVolumeDelta));
        var usageOrShot = Math.Abs(usage - 1m) >= Math.Abs(shotVolume - 1m)
            ? usage
            : shotVolume;
        var assists = AggregateMultiplier(
            applicableImpacts.Select(value => value.AssistShareDelta));
        var rebounds = AggregateMultiplier(
            applicableImpacts.Select(value => value.ReboundShareDelta));

        var values = baseline.PerMinuteRates.Values.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var multiplier = UsageStats.Contains(entry.Key)
                    ? usageOrShot
                    : entry.Key == StatKey.AST
                        ? assists
                        : ReboundStats.Contains(entry.Key)
                            ? rebounds
                            : 1m;
                return entry.Key == StatKey.MIN
                    ? minutes
                    : entry.Value * multiplier * minutes;
            });
        values[StatKey.MIN] = minutes;

        var roleRisk = Math.Clamp(
            applicableImpacts.Sum(value => value.RoleRiskDelta),
            0m,
            1m);
        var contextCertainty = Math.Clamp(
            CalculateContextCertainty(applicableEvents)
                + applicableImpacts.Sum(value => value.ProjectionConfidenceDelta),
            0m,
            1m);
        var confidence = confidenceCalculator.Calculate(new ConfidenceFactors(
            SampleSize: 0.5m,
            RoleStability: 1m - roleRisk,
            DataFreshness: 0.5m,
            SourceQuality: 0.5m,
            ContextCertainty: contextCertainty));

        return new AdjustedProjection(
            adjustedProjectionId,
            baseline.PlayerId,
            baseline.Id,
            new StatLine(values),
            applicableEvents.Select(value => value.Id).ToArray(),
            roleRisk,
            confidence,
            contextCertainty,
            applicableEvents.Any(value =>
                value.Verification == VerificationState.Proposed),
            computedAt);
    }

    private static decimal AggregateMultiplier(IEnumerable<decimal> deltas)
    {
        var aggregate = deltas.Sum(delta => Math.Clamp(delta, -0.5m, 0.5m));
        return Math.Clamp(1m + aggregate, 0.5m, 1.5m);
    }

    private static decimal CalculateContextCertainty(
        IReadOnlyList<ContextEvent> events)
    {
        if (events.Count == 0)
        {
            return 1m;
        }

        return events.Average(contextEvent =>
        {
            var certainty = contextEvent.Confidence switch
            {
                Confidence.High => 1m,
                Confidence.Moderate => 0.75m,
                Confidence.Low => 0.50m,
                Confidence.Speculative => 0.25m,
                _ => throw new ArgumentOutOfRangeException(nameof(events)),
            };
            return contextEvent.Verification == VerificationState.Proposed
                ? certainty * 0.5m
                : certainty;
        });
    }
}
