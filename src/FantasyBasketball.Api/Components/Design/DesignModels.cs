using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Api.Components.Design;

public sealed record PlayerTableItem(
    PlayerId Id,
    string Name,
    string Positions,
    decimal? Value,
    decimal? ValueAboveReplacement,
    decimal? AverageDraftPosition,
    bool IsRecommended,
    bool HasUnverifiedContext,
    IReadOnlyList<RecommendationEvidence> Evidence,
    string? Href = null);

public sealed record ProjectionStageItem(
    string Label,
    decimal Value,
    string Detail);

public sealed record CategoryProfileItem(
    string Category,
    decimal ZScore,
    bool IsPunted);

public sealed record TrendPoint(
    string Label,
    decimal Value);

public sealed record CalibrationPoint(
    decimal Predicted,
    decimal Actual);

public sealed record PickEntryItem(
    PlayerId Id,
    string Name,
    string Positions);

public sealed record DraftBoardItem(
    PlayerTableItem Player,
    Confidence Confidence);

public sealed record SourceHealthItem(
    string Source,
    bool IsDegraded,
    bool IsStale,
    string Age,
    string Detail);
