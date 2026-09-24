using FantasyBasketball.Domain.Accounts;

namespace FantasyBasketball.Infrastructure.Persistence.Entities;

public sealed class PlayerRow
{
    private PlayerRow()
    {
    }

    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public Guid? CurrentTeamId { get; private set; }

    public string[] Positions { get; private set; } = [];

    public DateOnly? BirthDate { get; private set; }

    public void MoveToTeam(Guid? teamId) => CurrentTeamId = teamId;

    public static PlayerRow Create(
        Guid id,
        string fullName,
        string normalizedName,
        string[] positions,
        DateOnly? birthDate,
        Guid? currentTeamId = null) =>
        new()
        {
            Id = id,
            FullName = fullName,
            NormalizedName = normalizedName,
            CurrentTeamId = currentTeamId,
            Positions = positions,
            BirthDate = birthDate,
        };
}

public sealed class NbaTeamRow
{
    private NbaTeamRow()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Abbreviation { get; private set; } = string.Empty;

    public static NbaTeamRow Create(Guid id, string name, string abbreviation) =>
        new()
        {
            Id = id,
            Name = name,
            Abbreviation = abbreviation,
        };
}

public sealed class NbaTeamSourceRow
{
    private NbaTeamSourceRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid NbaTeamId { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset? SourceTimestamp { get; private set; }

    public string ParserVersion { get; private set; } = string.Empty;

    public decimal Confidence { get; private set; }

    public string RawRecordHash { get; private set; } = string.Empty;

    public static NbaTeamSourceRow Create(
        Guid nbaTeamId,
        string source,
        string externalId,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            NbaTeamId = nbaTeamId,
            Source = source,
            ExternalId = externalId,
            FetchedAt = fetchedAt,
            SourceTimestamp = sourceTimestamp,
            ParserVersion = parserVersion,
            Confidence = confidence,
            RawRecordHash = rawRecordHash,
        };
}

public sealed class ExternalPlayerIdentityRow
{
    private ExternalPlayerIdentityRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public DateTimeOffset LinkedAt { get; private set; }

    public bool ConfirmedByHuman { get; private set; }

    public static ExternalPlayerIdentityRow Create(
        Guid playerId,
        string provider,
        string externalId,
        DateTimeOffset? linkedAt = null,
        bool confirmedByHuman = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Provider = provider,
            ExternalId = externalId,
            LinkedAt = linkedAt ?? DateTimeOffset.UnixEpoch,
            ConfirmedByHuman = confirmedByHuman,
        };
}

public sealed class PendingIdentityMatchRow
{
    private PendingIdentityMatchRow()
    {
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public Guid[] CandidatePlayerIds { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public static PendingIdentityMatchRow Create(
        Guid id,
        string provider,
        string externalId,
        string fullName,
        string normalizedName,
        Guid[] candidatePlayerIds,
        DateTimeOffset createdAt,
        string reason) =>
        new()
        {
            Id = id,
            Provider = provider,
            ExternalId = externalId,
            FullName = fullName,
            NormalizedName = normalizedName,
            CandidatePlayerIds = candidatePlayerIds,
            CreatedAt = createdAt,
            Reason = reason,
        };
}

public sealed class DataImportRunRow
{
    private DataImportRunRow()
    {
    }

    public Guid Id { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string Status { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public int RowsWritten { get; private set; }

    public int PendingIdentityMatches { get; private set; }

    public string? FailureDetail { get; private set; }

    public static DataImportRunRow Create(
        Guid id,
        string source,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        int rowsWritten,
        int pendingIdentityMatches,
        string? failureDetail) =>
        new()
        {
            Id = id,
            Source = source,
            Status = status,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            RowsWritten = rowsWritten,
            PendingIdentityMatches = pendingIdentityMatches,
            FailureDetail = failureDetail,
        };
}

public sealed class FantasyLeagueRow : IOwnedResource
{
    private FantasyLeagueRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Type { get; private set; } = string.Empty;

    public int TeamCount { get; private set; }

    public string[] Categories { get; private set; } = [];

    public string Cadence { get; private set; } = string.Empty;

    public List<ScoringRuleRow> ScoringRules { get; private set; } = [];

    public List<RosterSlotRow> RosterSlots { get; private set; } = [];

    public static FantasyLeagueRow Create(
        Guid id,
        string name,
        string type,
        int teamCount,
        string[] categories,
        string cadence) =>
        new()
        {
            Id = id,
            Name = name,
            Type = type,
            TeamCount = teamCount,
            Categories = categories,
            Cadence = cadence,
        };

    public void UpdateSettings(string name, int teamCount, string cadence)
    {
        Name = name;
        TeamCount = teamCount;
        Cadence = cadence;
    }
}

public sealed class ScoringRuleRow : IOwnedResource
{
    private ScoringRuleRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid FantasyLeagueId { get; private set; }

    public string Stat { get; private set; } = string.Empty;

    public decimal PointsPerUnit { get; private set; }

    public int Ordinal { get; private set; }

    public static ScoringRuleRow Create(
        Guid fantasyLeagueId,
        string stat,
        decimal pointsPerUnit,
        int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            FantasyLeagueId = fantasyLeagueId,
            Stat = stat,
            PointsPerUnit = pointsPerUnit,
            Ordinal = ordinal,
        };
}

public sealed class RosterSlotRow : IOwnedResource
{
    private RosterSlotRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid FantasyLeagueId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public int Ordinal { get; private set; }

    public static RosterSlotRow Create(Guid fantasyLeagueId, string kind, int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            FantasyLeagueId = fantasyLeagueId,
            Kind = kind,
            Ordinal = ordinal,
        };
}

public sealed class LeagueTeamRow : IOwnedResource
{
    private LeagueTeamRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid FantasyLeagueId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsUsersTeam { get; private set; }

    public int Ordinal { get; private set; }

    public List<LeagueRosterEntryRow> Entries { get; private set; } = [];

    public static LeagueTeamRow Create(Guid id, Guid fantasyLeagueId, string name, bool isUsersTeam, int ordinal) =>
        new() { Id = id, FantasyLeagueId = fantasyLeagueId, Name = name, IsUsersTeam = isUsersTeam, Ordinal = ordinal };
}

public sealed class LeagueRosterEntryRow : IOwnedResource
{
    private LeagueRosterEntryRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid LeagueTeamId { get; private set; }

    public Guid PlayerId { get; private set; }

    public int Ordinal { get; private set; }

    public static LeagueRosterEntryRow Create(Guid leagueTeamId, Guid playerId, int ordinal) =>
        new() { Id = Guid.NewGuid(), LeagueTeamId = leagueTeamId, PlayerId = playerId, Ordinal = ordinal };
}

public sealed class SeasonStatLineRow
{
    private SeasonStatLineRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public int SeasonEndYear { get; private set; }

    public int GamesPlayed { get; private set; }

    public decimal MinutesPerGame { get; private set; }

    public string PerGame { get; private set; } = "{}";

    public string Totals { get; private set; } = "{}";

    public decimal? UsageRate { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string? ExternalId { get; private set; }

    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset? SourceTimestamp { get; private set; }

    public string ParserVersion { get; private set; } = string.Empty;

    public decimal Confidence { get; private set; }

    public string RawRecordHash { get; private set; } = string.Empty;

    public static SeasonStatLineRow Create(
        Guid playerId,
        int seasonEndYear,
        int gamesPlayed,
        decimal minutesPerGame,
        string perGame,
        string totals,
        decimal? usageRate,
        string source,
        string? externalId,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            SeasonEndYear = seasonEndYear,
            GamesPlayed = gamesPlayed,
            MinutesPerGame = minutesPerGame,
            PerGame = perGame,
            Totals = totals,
            UsageRate = usageRate,
            Source = source,
            ExternalId = externalId,
            FetchedAt = fetchedAt,
            SourceTimestamp = sourceTimestamp,
            ParserVersion = parserVersion,
            Confidence = confidence,
            RawRecordHash = rawRecordHash,
        };
}

public sealed class NbaGameRow
{
    private NbaGameRow()
    {
    }

    public Guid Id { get; private set; }

    public int SeasonEndYear { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public Guid HomeTeamId { get; private set; }

    public Guid AwayTeamId { get; private set; }

    public int? HomeScore { get; private set; }

    public int? AwayScore { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public string Source { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset? SourceTimestamp { get; private set; }

    public string ParserVersion { get; private set; } = string.Empty;

    public decimal Confidence { get; private set; }

    public string RawRecordHash { get; private set; } = string.Empty;

    public void RecordResult(
        DateTimeOffset startsAt,
        int? homeScore,
        int? awayScore,
        string status,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash)
    {
        StartsAt = startsAt;
        HomeScore = homeScore;
        AwayScore = awayScore;
        Status = status;
        FetchedAt = fetchedAt;
        SourceTimestamp = sourceTimestamp;
        ParserVersion = parserVersion;
        Confidence = confidence;
        RawRecordHash = rawRecordHash;
    }

    public static NbaGameRow Create(
        Guid id,
        int seasonEndYear,
        DateTimeOffset startsAt,
        Guid homeTeamId,
        Guid awayTeamId,
        int? homeScore,
        int? awayScore,
        string status,
        string source,
        string externalId,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash) =>
        new()
        {
            Id = id,
            SeasonEndYear = seasonEndYear,
            StartsAt = startsAt,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = status,
            Source = source,
            ExternalId = externalId,
            FetchedAt = fetchedAt,
            SourceTimestamp = sourceTimestamp,
            ParserVersion = parserVersion,
            Confidence = confidence,
            RawRecordHash = rawRecordHash,
        };
}

public sealed class AdpEntryRow
{
    private AdpEntryRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public decimal AverageDraftPosition { get; private set; }

    public decimal? StandardDeviation { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public string ExternalId { get; private set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; private set; }

    public DateTimeOffset? SourceTimestamp { get; private set; }

    public string ParserVersion { get; private set; } = string.Empty;

    public decimal Confidence { get; private set; }

    public string RawRecordHash { get; private set; } = string.Empty;

    public static AdpEntryRow Create(
        Guid id,
        Guid playerId,
        decimal averageDraftPosition,
        decimal? standardDeviation,
        string source,
        string externalId,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash) =>
        new()
        {
            Id = id,
            PlayerId = playerId,
            AverageDraftPosition = averageDraftPosition,
            StandardDeviation = standardDeviation,
            Source = source,
            ExternalId = externalId,
            FetchedAt = fetchedAt,
            SourceTimestamp = sourceTimestamp,
            ParserVersion = parserVersion,
            Confidence = confidence,
            RawRecordHash = rawRecordHash,
        };
}

public sealed class BaselineProjectionRow
{
    private BaselineProjectionRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public Guid? ObservedStatsId { get; private set; }

    public decimal ProjectedMinutesPerGame { get; private set; }

    public string PerMinuteRates { get; private set; } = "{}";

    public string ProjectedPerGame { get; private set; } = "{}";

    public int ProjectedGamesPlayed { get; private set; }

    public string ModelVersion { get; set; } = string.Empty;

    public DateTimeOffset ComputedAt { get; private set; }

    public static BaselineProjectionRow Create(
        Guid id,
        Guid playerId,
        decimal projectedMinutesPerGame,
        string perMinuteRates,
        string projectedPerGame,
        int projectedGamesPlayed,
        DateTimeOffset computedAt,
        string modelVersion,
        Guid? observedStatsId = null) =>
        new()
        {
            Id = id,
            PlayerId = playerId,
            ProjectedMinutesPerGame = projectedMinutesPerGame,
            PerMinuteRates = perMinuteRates,
            ProjectedPerGame = projectedPerGame,
            ProjectedGamesPlayed = projectedGamesPlayed,
            ModelVersion = modelVersion,
            ObservedStatsId = observedStatsId,
            ComputedAt = computedAt,
        };
}

public sealed class ObservedStatsRow
{
    private ObservedStatsRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid PlayerId { get; private set; }

    public int SeasonEndYear { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset AsOf { get; private set; }

    public static ObservedStatsRow Create(
        Guid playerId,
        int seasonEndYear,
        string source,
        DateTimeOffset asOf) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            SeasonEndYear = seasonEndYear,
            Source = source,
            AsOf = asOf,
        };
}

public sealed class DraftSessionRow : IOwnedResource
{
    private DraftSessionRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid FantasyLeagueId { get; private set; }

    public int RoundCount { get; private set; }

    public int TeamCount { get; private set; }

    public int UserSlot { get; private set; }

    public static DraftSessionRow Create(
        Guid id,
        Guid fantasyLeagueId,
        int roundCount,
        int teamCount,
        int userSlot) =>
        new()
        {
            Id = id,
            FantasyLeagueId = fantasyLeagueId,
            RoundCount = roundCount,
            TeamCount = teamCount,
            UserSlot = userSlot,
        };
}

public sealed class DraftPickRow : IOwnedResource
{
    private DraftPickRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid DraftSessionId { get; private set; }

    public Guid PlayerId { get; private set; }

    public int PickNumber { get; private set; }

    public static DraftPickRow Create(
        Guid id,
        Guid draftSessionId,
        Guid playerId,
        int pickNumber) =>
        new()
        {
            Id = id,
            DraftSessionId = draftSessionId,
            PlayerId = playerId,
            PickNumber = pickNumber,
        };
}

public sealed class ContextEventRow : IOwnedResource
{
    private ContextEventRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public Guid? TeamId { get; private set; }

    public Guid? PrimaryPlayerId { get; private set; }

    public Guid[] AffectedPlayerIds { get; private set; } = [];

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? ExpectedExpiration { get; private set; }

    public string Direction { get; private set; } = string.Empty;

    public decimal Magnitude { get; private set; }

    public string Confidence { get; private set; } = string.Empty;

    public string? SourceUrl { get; private set; }

    public string SourceName { get; private set; } = string.Empty;

    public string? RawText { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string Verification { get; private set; } = string.Empty;

    public Guid? ReviewedByUserId { get; private set; }

    public Guid? VerifiedByUserId { get; private set; }

    public DateTimeOffset? VerifiedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public List<PlayerContextImpactRow> Impacts { get; private set; } = [];

    public static ContextEventRow Create(
        Guid id,
        string type,
        Guid? teamId,
        Guid? primaryPlayerId,
        Guid[] affectedPlayerIds,
        DateTimeOffset createdAt,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? expectedExpiration,
        string direction,
        decimal magnitude,
        string confidence,
        string? sourceUrl,
        string sourceName,
        string? rawText,
        string summary,
        string verification) =>
        new()
        {
            Id = id,
            Type = type,
            TeamId = teamId,
            PrimaryPlayerId = primaryPlayerId,
            AffectedPlayerIds = affectedPlayerIds,
            CreatedAt = createdAt,
            EffectiveFrom = effectiveFrom,
            ExpectedExpiration = expectedExpiration,
            Direction = direction,
            Magnitude = magnitude,
            Confidence = confidence,
            SourceUrl = sourceUrl,
            SourceName = sourceName,
            RawText = rawText,
            Summary = summary,
            Verification = verification,
        };

    public void ApplyReview(
        DateTimeOffset? expectedExpiration,
        string verification,
        Guid? reviewedByUserId,
        Guid? verifiedByUserId,
        DateTimeOffset? verifiedAt,
        DateTimeOffset? reviewedAt)
    {
        ExpectedExpiration = expectedExpiration;
        Verification = verification;
        ReviewedByUserId = reviewedByUserId;
        VerifiedByUserId = verifiedByUserId;
        VerifiedAt = verifiedAt;
        ReviewedAt = reviewedAt;
    }
}

public sealed class PlayerContextImpactRow : IOwnedResource
{
    private PlayerContextImpactRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid ContextEventId { get; private set; }

    public Guid PlayerId { get; private set; }

    public decimal MinutesDelta { get; private set; }

    public decimal UsageDelta { get; private set; }

    public decimal AssistShareDelta { get; private set; }

    public decimal ReboundShareDelta { get; private set; }

    public decimal ShotVolumeDelta { get; private set; }

    public decimal RoleRiskDelta { get; private set; }

    public decimal ProjectionConfidenceDelta { get; private set; }

    public bool IsOverridden { get; private set; }

    public Guid? OverrideUserId { get; private set; }

    public DateTimeOffset? OverriddenAt { get; private set; }

    public static PlayerContextImpactRow Create(
        Guid id,
        Guid contextEventId,
        Guid playerId,
        decimal minutesDelta,
        decimal usageDelta,
        decimal assistShareDelta,
        decimal reboundShareDelta,
        decimal shotVolumeDelta,
        decimal roleRiskDelta,
        decimal projectionConfidenceDelta,
        bool isOverridden) =>
        new()
        {
            Id = id,
            ContextEventId = contextEventId,
            PlayerId = playerId,
            MinutesDelta = minutesDelta,
            UsageDelta = usageDelta,
            AssistShareDelta = assistShareDelta,
            ReboundShareDelta = reboundShareDelta,
            ShotVolumeDelta = shotVolumeDelta,
            RoleRiskDelta = roleRiskDelta,
            ProjectionConfidenceDelta = projectionConfidenceDelta,
            IsOverridden = isOverridden,
        };

    public void ApplyOverride(
        decimal minutesDelta,
        decimal usageDelta,
        decimal assistShareDelta,
        decimal reboundShareDelta,
        decimal shotVolumeDelta,
        decimal roleRiskDelta,
        decimal projectionConfidenceDelta,
        Guid userId,
        DateTimeOffset overriddenAt)
    {
        MinutesDelta = minutesDelta;
        UsageDelta = usageDelta;
        AssistShareDelta = assistShareDelta;
        ReboundShareDelta = reboundShareDelta;
        ShotVolumeDelta = shotVolumeDelta;
        RoleRiskDelta = roleRiskDelta;
        ProjectionConfidenceDelta = projectionConfidenceDelta;
        IsOverridden = true;
        OverrideUserId = userId;
        OverriddenAt = overriddenAt;
    }
}

public sealed class AdjustedProjectionRow : IGlobalOrOwnedResource
{
    private AdjustedProjectionRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid PlayerId { get; private set; }

    public Guid BaselineProjectionId { get; private set; }

    public string ProjectedPerGame { get; private set; } = "{}";

    public Guid[] AppliedContextEventIds { get; private set; } = [];

    public decimal RoleRisk { get; private set; }

    public string Confidence { get; private set; } = string.Empty;

    public decimal ContextCertainty { get; private set; }

    public bool HasUnverifiedContext { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public static AdjustedProjectionRow Create(
        Guid id,
        Guid? ownerId,
        Guid playerId,
        Guid baselineProjectionId,
        string projectedPerGame,
        Guid[] appliedContextEventIds,
        decimal roleRisk,
        string confidence,
        decimal contextCertainty,
        bool hasUnverifiedContext,
        DateTimeOffset computedAt) =>
        new()
        {
            Id = id,
            OwnerId = ownerId,
            PlayerId = playerId,
            BaselineProjectionId = baselineProjectionId,
            ProjectedPerGame = projectedPerGame,
            AppliedContextEventIds = appliedContextEventIds,
            RoleRisk = roleRisk,
            Confidence = confidence,
            ContextCertainty = contextCertainty,
            HasUnverifiedContext = hasUnverifiedContext,
            ComputedAt = computedAt,
        };
}

public sealed class FantasyValueRow : IOwnedResource
{
    private FantasyValueRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid PlayerId { get; private set; }

    public Guid FantasyLeagueId { get; private set; }

    public decimal PerGame { get; private set; }

    public decimal SeasonTotal { get; private set; }

    public Guid? AdjustedProjectionId { get; private set; }

    public DateTimeOffset? ComputedAt { get; private set; }

    public string? ScoringProfile { get; private set; }

    public Guid? PublicationId { get; private set; }

    public static FantasyValueRow Create(
        Guid id,
        Guid playerId,
        Guid fantasyLeagueId,
        decimal perGame,
        decimal seasonTotal,
        Guid? adjustedProjectionId,
        DateTimeOffset? computedAt = null,
        string? scoringProfile = null,
        Guid? publicationId = null) =>
        new()
        {
            Id = id,
            PlayerId = playerId,
            FantasyLeagueId = fantasyLeagueId,
            PerGame = perGame,
            SeasonTotal = seasonTotal,
            AdjustedProjectionId = adjustedProjectionId,
            ComputedAt = computedAt,
            ScoringProfile = scoringProfile,
            PublicationId = publicationId,
        };
}

public sealed class RecommendationRow : IOwnedResource
{
    private RecommendationRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public Guid? SubjectPlayerId { get; private set; }

    public decimal Score { get; private set; }

    public string Confidence { get; private set; } = string.Empty;

    public List<RecommendationEvidenceRow> Evidence { get; private set; } = [];

    public static RecommendationRow Create(
        Guid id,
        string action,
        Guid? subjectPlayerId,
        decimal score,
        string confidence) =>
        new()
        {
            Id = id,
            Action = action,
            SubjectPlayerId = subjectPlayerId,
            Score = score,
            Confidence = confidence,
        };
}

public sealed class RecommendationEvidenceRow : IOwnedResource
{
    private RecommendationEvidenceRow()
    {
    }

    public Guid Id { get; private set; }

    public Guid? OwnerId { get; private set; }

    public Guid RecommendationId { get; private set; }

    public string Kind { get; private set; } = string.Empty;

    public string Polarity { get; private set; } = string.Empty;

    public string Statement { get; private set; } = string.Empty;

    public decimal? Magnitude { get; private set; }

    public int Ordinal { get; private set; }

    public static RecommendationEvidenceRow Create(
        Guid recommendationId,
        string kind,
        string polarity,
        string statement,
        decimal? magnitude,
        int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            RecommendationId = recommendationId,
            Kind = kind,
            Polarity = polarity,
            Statement = statement,
            Magnitude = magnitude,
            Ordinal = ordinal,
        };
}
