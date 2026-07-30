using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Players;

namespace FantasyBasketball.Domain.Recommendations;

public enum EvidenceKind
{
    Minutes,
    Usage,
    Opportunity,
    Efficiency,
    Schedule,
    RosterFit,
    Scarcity,
    Market,
    Context,
    Injury,
    SampleSize,
    DataQuality,
}

public enum EvidencePolarity
{
    Supporting,
    Risk,
    Neutral,
}

public enum Confidence
{
    High,
    Moderate,
    Low,
    Speculative,
}

public sealed record RecommendationEvidence
{
    public RecommendationEvidence(
        EvidenceKind kind,
        EvidencePolarity polarity,
        string statement,
        decimal? magnitude)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        Kind = kind;
        Polarity = polarity;
        Statement = statement.Trim();
        Magnitude = magnitude;
    }

    public EvidenceKind Kind { get; }

    public EvidencePolarity Polarity { get; }

    public string Statement { get; }

    public decimal? Magnitude { get; }
}

public sealed record Recommendation
{
    public Recommendation(
        Guid id,
        string action,
        PlayerId? subjectPlayerId,
        decimal score,
        Confidence confidence,
        IReadOnlyList<RecommendationEvidence> evidence)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Recommendation id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0)
        {
            throw new ArgumentException(
                "A recommendation requires at least one evidence item.",
                nameof(evidence));
        }

        Id = id;
        Action = action.Trim();
        SubjectPlayerId = subjectPlayerId;
        Score = score;
        Confidence = confidence;
        Evidence = new ReadOnlyCollection<RecommendationEvidence>(
            EvidenceOrderer.Order(evidence).ToArray());
    }

    public Guid Id { get; }

    public string Action { get; }

    public PlayerId? SubjectPlayerId { get; }

    public decimal Score { get; }

    public Confidence Confidence { get; }

    public IReadOnlyList<RecommendationEvidence> Evidence { get; }
}

public sealed record ConfidenceFactors(
    decimal SampleSize,
    decimal RoleStability,
    decimal DataFreshness,
    decimal SourceQuality,
    decimal ContextCertainty);

public sealed class ConfidenceCalculator
{
    public Confidence Calculate(ConfidenceFactors factors)
    {
        ArgumentNullException.ThrowIfNull(factors);
        var score = (Clamp(factors.SampleSize) * 0.30m)
            + (Clamp(factors.RoleStability) * 0.25m)
            + (Clamp(factors.DataFreshness) * 0.20m)
            + (Clamp(factors.SourceQuality) * 0.15m)
            + (Clamp(factors.ContextCertainty) * 0.10m);
        return score switch
        {
            >= 0.75m => Confidence.High,
            >= 0.55m => Confidence.Moderate,
            >= 0.35m => Confidence.Low,
            _ => Confidence.Speculative,
        };
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);
}

public static class EvidenceOrderer
{
    public static IReadOnlyList<RecommendationEvidence> Order(
        IReadOnlyList<RecommendationEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return evidence
            .OrderBy(item => item.Polarity switch
            {
                EvidencePolarity.Supporting => 0,
                EvidencePolarity.Neutral => 1,
                EvidencePolarity.Risk => 2,
                _ => 3,
            })
            .ThenByDescending(item => Math.Abs(item.Magnitude ?? 0m))
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Statement, StringComparer.Ordinal)
            .ToArray();
    }
}
