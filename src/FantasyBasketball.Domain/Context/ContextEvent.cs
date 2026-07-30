using System.Collections.ObjectModel;
using FantasyBasketball.Domain.Players;
using FantasyBasketball.Domain.Recommendations;

namespace FantasyBasketball.Domain.Context;

public enum ContextEventType
{
    Trade,
    Signing,
    Injury,
    ReturnFromInjury,
    StartingLineupChange,
    BenchRoleChange,
    MinutesRestriction,
    CoachStatement,
    FacilitatorChange,
    UsageChange,
    PositionChange,
    RotationChange,
    RestRisk,
    DepthChartChange,
}

public enum ContextDirection
{
    Positive,
    Negative,
    Neutral,
}

public enum VerificationState
{
    Proposed,
    Verified,
    Rejected,
}

public sealed record ContextEvent
{
    private ContextEvent(
        Guid id,
        ContextEventType type,
        NbaTeamId? teamId,
        PlayerId? primaryPlayerId,
        IReadOnlyList<PlayerId> affectedPlayerIds,
        DateTimeOffset createdAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? expectedExpiration,
        ContextDirection direction,
        decimal magnitude,
        Confidence confidence,
        string? sourceUrl,
        string sourceName,
        string? rawText,
        string summary)
    {
        Id = id;
        Type = type;
        TeamId = teamId;
        PrimaryPlayerId = primaryPlayerId;
        AffectedPlayerIds = new ReadOnlyCollection<PlayerId>(
            affectedPlayerIds.Distinct().ToArray());
        CreatedAt = createdAt;
        EffectiveFrom = effectiveFrom;
        ExpectedExpiration = expectedExpiration;
        Direction = direction;
        Magnitude = magnitude;
        Confidence = confidence;
        SourceUrl = sourceUrl;
        SourceName = sourceName;
        RawText = rawText;
        Summary = summary;
    }

    public Guid Id { get; }

    public ContextEventType Type { get; }

    public NbaTeamId? TeamId { get; }

    public PlayerId? PrimaryPlayerId { get; }

    public IReadOnlyList<PlayerId> AffectedPlayerIds { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset EffectiveFrom { get; }

    public DateTimeOffset? ExpectedExpiration { get; private set; }

    public ContextDirection Direction { get; }

    public decimal Magnitude { get; }

    public Confidence Confidence { get; }

    public string? SourceUrl { get; }

    public string SourceName { get; }

    public string? RawText { get; }

    public string Summary { get; }

    public VerificationState Verification { get; private set; } =
        VerificationState.Proposed;

    public Guid? ReviewedByUserId { get; private set; }

    public Guid? VerifiedByUserId { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public static ContextEvent Create(
        Guid id,
        ContextEventType type,
        NbaTeamId? teamId,
        PlayerId? primaryPlayerId,
        IReadOnlyList<PlayerId> affectedPlayerIds,
        DateTimeOffset createdAt,
        DateTimeOffset effectiveFrom,
        ContextDirection direction,
        decimal magnitude,
        Confidence confidence,
        string? sourceUrl,
        string sourceName,
        string? rawText,
        string summary,
        DateTimeOffset? expectedExpiration = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Context event id cannot be empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(affectedPlayerIds);
        if (affectedPlayerIds.Count == 0)
        {
            throw new ArgumentException(
                "A context event requires at least one affected player.",
                nameof(affectedPlayerIds));
        }

        EnsureUtc(createdAt, nameof(createdAt));
        EnsureUtc(effectiveFrom, nameof(effectiveFrom));
        if (expectedExpiration is { } expiration)
        {
            EnsureUtc(expiration, nameof(expectedExpiration));
            if (expiration < effectiveFrom)
            {
                throw new ArgumentException(
                    "Expected expiration cannot precede the effective time.",
                    nameof(expectedExpiration));
            }
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        if (sourceUrl is not null
            && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp
                    && parsed.Scheme != Uri.UriSchemeHttps)))
        {
            throw new ArgumentException(
                "Source URL must be an absolute HTTP or HTTPS URL.",
                nameof(sourceUrl));
        }

        var defaultExpiration =
            ContextEventCatalog.GetDefaults(type).DefaultExpiration;
        var expirationValue = expectedExpiration
            ?? (defaultExpiration is { } duration
                ? effectiveFrom.Add(duration)
                : null);

        return new ContextEvent(
            id,
            type,
            teamId,
            primaryPlayerId,
            affectedPlayerIds,
            createdAt,
            effectiveFrom,
            expirationValue,
            direction,
            Math.Clamp(magnitude, 0m, 1m),
            confidence,
            sourceUrl,
            sourceName.Trim(),
            string.IsNullOrWhiteSpace(rawText) ? null : rawText.Trim(),
            summary.Trim());
    }

    public void VerifyByHuman(Guid userId, DateTimeOffset verifiedAt)
    {
        EnsureHumanReview(userId, verifiedAt);
        Verification = VerificationState.Verified;
        VerifiedByUserId = userId;
        VerifiedAt = verifiedAt;
    }

    public void RejectByHuman(Guid userId, DateTimeOffset rejectedAt)
    {
        EnsureHumanReview(userId, rejectedAt);
        Verification = VerificationState.Rejected;
        VerifiedByUserId = null;
        VerifiedAt = null;
    }

    public void ExpireByHuman(Guid userId, DateTimeOffset expiredAt)
    {
        EnsureHumanReview(userId, expiredAt);
        ExpectedExpiration = expiredAt;
    }

    private void EnsureHumanReview(Guid userId, DateTimeOffset reviewedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer user id cannot be empty.", nameof(userId));
        }

        EnsureUtc(reviewedAt, nameof(reviewedAt));
        ReviewedByUserId = userId;
        ReviewedAt = reviewedAt;
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }
    }
}
