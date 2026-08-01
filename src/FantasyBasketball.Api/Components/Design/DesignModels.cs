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

public enum MoveDirection
{
    Riser,
    Faller,
}

public sealed record MoverItem(
    string Name,
    string Team,
    string Positions,
    int Rank,
    int PreviousRank,
    MoveDirection Direction,
    string Reason)
{
    public int RankDelta => PreviousRank - Rank;
}

public sealed record MoverBoard(
    string Title,
    string ScoringLabel,
    IReadOnlyList<MoverItem> Movers);

public sealed record RankedPlayerItem(
    int Rank,
    string Name,
    string Team,
    string Positions,
    decimal Value);

public sealed record CoverageItem(
    string Headline,
    string Outlet,
    string Age,
    string Summary);

public sealed record ImportPlatformItem(
    string Name,
    string Support);

public sealed record OnboardingStepItem(
    string Title,
    string Description);

/// <summary>
/// One entry in the active-league switcher. A flat pair rather than the domain
/// league, so the switcher stays presentational and cannot be handed something
/// it might be tempted to compute from.
/// </summary>
public sealed record LeagueOption(
    Guid Id,
    string Name);
