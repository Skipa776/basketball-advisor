using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;
using FantasyBasketball.Domain.Stats;

namespace FantasyBasketball.Domain.Projections;

public sealed record ObservedStats
{
    public ObservedStats(
        PlayerId playerId,
        SeasonStatLine source,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.PlayerId != playerId)
        {
            throw new ArgumentException(
                "Observed stats player must match the source stat line.",
                nameof(playerId));
        }

        if (asOf.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("AsOf must be UTC.", nameof(asOf));
        }

        PlayerId = playerId;
        Source = source;
        AsOf = asOf;
    }

    public PlayerId PlayerId { get; }

    public SeasonStatLine Source { get; }

    public DateTimeOffset AsOf { get; }
}

public sealed record BaselineProjection
{
    public BaselineProjection(
        Guid id,
        PlayerId playerId,
        decimal projectedMinutesPerGame,
        StatLine perMinuteRates,
        StatLine projectedPerGame,
        int projectedGamesPlayed,
        DateTimeOffset computedAt,
        string modelVersion)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Baseline projection id cannot be empty.",
                nameof(id));
        }

        if (projectedMinutesPerGame is < 0m or > 42m)
        {
            throw new ArgumentOutOfRangeException(nameof(projectedMinutesPerGame));
        }

        if (projectedGamesPlayed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projectedGamesPlayed));
        }

        if (computedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("ComputedAt must be UTC.", nameof(computedAt));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(modelVersion);
        Id = id;
        PlayerId = playerId;
        ProjectedMinutesPerGame = projectedMinutesPerGame;
        PerMinuteRates = perMinuteRates
            ?? throw new ArgumentNullException(nameof(perMinuteRates));
        ProjectedPerGame = projectedPerGame
            ?? throw new ArgumentNullException(nameof(projectedPerGame));
        ProjectedGamesPlayed = projectedGamesPlayed;
        ComputedAt = computedAt;
        ModelVersion = modelVersion;
    }

    public Guid Id { get; }

    public PlayerId PlayerId { get; }

    public decimal ProjectedMinutesPerGame { get; }

    public StatLine PerMinuteRates { get; }

    public StatLine ProjectedPerGame { get; }

    public int ProjectedGamesPlayed { get; }

    public DateTimeOffset ComputedAt { get; }

    public string ModelVersion { get; }
}

public sealed record AdjustedProjection
{
    public AdjustedProjection(
        Guid id,
        PlayerId playerId,
        Guid baselineProjectionId,
        StatLine projectedPerGame,
        IReadOnlyList<Guid> appliedContextEventIds,
        decimal roleRisk,
        Confidence confidence,
        decimal contextCertainty,
        bool hasUnverifiedContext,
        DateTimeOffset computedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Adjusted projection id cannot be empty.",
                nameof(id));
        }

        if (baselineProjectionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Baseline projection id cannot be empty.",
                nameof(baselineProjectionId));
        }

        ArgumentNullException.ThrowIfNull(projectedPerGame);
        ArgumentNullException.ThrowIfNull(appliedContextEventIds);
        if (computedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("ComputedAt must be UTC.", nameof(computedAt));
        }

        Id = id;
        PlayerId = playerId;
        BaselineProjectionId = baselineProjectionId;
        ProjectedPerGame = projectedPerGame;
        AppliedContextEventIds = appliedContextEventIds.ToArray();
        RoleRisk = Math.Clamp(roleRisk, 0m, 1m);
        Confidence = confidence;
        ContextCertainty = Math.Clamp(contextCertainty, 0m, 1m);
        HasUnverifiedContext = hasUnverifiedContext;
        ComputedAt = computedAt;
    }

    public Guid Id { get; }

    public PlayerId PlayerId { get; }

    public Guid BaselineProjectionId { get; }

    public StatLine ProjectedPerGame { get; }

    public IReadOnlyList<Guid> AppliedContextEventIds { get; }

    public decimal RoleRisk { get; }

    public Confidence Confidence { get; }

    public decimal ContextCertainty { get; }

    public bool HasUnverifiedContext { get; }

    public DateTimeOffset ComputedAt { get; }
}

public sealed record FantasyValue
{
    public FantasyValue(
        PlayerId playerId,
        Guid leagueId,
        decimal perGame,
        decimal seasonTotal,
        Guid? adjustedProjectionId,
        decimal? perGameSd = null)
    {
        if (leagueId == Guid.Empty)
        {
            throw new ArgumentException("League id cannot be empty.", nameof(leagueId));
        }

        PlayerId = playerId;
        LeagueId = leagueId;
        PerGame = perGame;
        SeasonTotal = seasonTotal;
        AdjustedProjectionId = adjustedProjectionId;
        PerGameSd = perGameSd is < 0m ? throw new ArgumentOutOfRangeException(nameof(perGameSd)) : perGameSd;
    }

    public PlayerId PlayerId { get; }

    public Guid LeagueId { get; }

    public decimal PerGame { get; }

    /// <summary>SD of fantasy points per game under the league's scoring, when a distribution was published.</summary>
    public decimal? PerGameSd { get; }

    public decimal SeasonTotal { get; }

    public Guid? AdjustedProjectionId { get; }
}
