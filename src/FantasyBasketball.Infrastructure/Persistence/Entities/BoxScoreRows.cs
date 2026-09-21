using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Domain.Trends;

namespace FantasyBasketball.Infrastructure.Persistence.Entities;

public sealed class BoxScoreSnapshotRow
{
    private BoxScoreSnapshotRow() { }
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public int SeasonEndYear { get; private set; }
    public DateOnly PlayedOn { get; private set; }
    public string Phase { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string? ExternalId { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset? SourceTimestamp { get; private set; }
    public string ParserVersion { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public string RawRecordHash { get; private set; } = string.Empty;

    public static BoxScoreSnapshotRow Create(Guid id, CompletedBoxScore snapshot) => new()
    {
        Id = id,
        GameId = snapshot.GameId,
        SeasonEndYear = snapshot.SeasonEndYear,
        PlayedOn = snapshot.PlayedOn,
        Phase = snapshot.Phase.ToString(),
        Source = snapshot.Provenance.Source,
        ExternalId = snapshot.Provenance.ExternalId,
        FetchedAt = snapshot.Provenance.FetchedAt,
        SourceTimestamp = snapshot.Provenance.SourceTimestamp,
        ParserVersion = snapshot.Provenance.ParserVersion,
        Confidence = snapshot.Provenance.Confidence,
        RawRecordHash = snapshot.Provenance.RawRecordHash,
    };
}

public sealed class PlayerGameStatRow
{
    private PlayerGameStatRow() { }
    public Guid Id { get; private set; }
    public Guid SnapshotId { get; private set; }
    public Guid PlayerId { get; private set; }
    public bool DidPlay { get; private set; }
    public string? Statistics { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string? ExternalId { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset? SourceTimestamp { get; private set; }
    public string ParserVersion { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public string RawRecordHash { get; private set; } = string.Empty;

    public static PlayerGameStatRow Create(Guid snapshotId, PlayerGameSample sample, string? statistics) => new()
    {
        Id = Guid.NewGuid(),
        SnapshotId = snapshotId,
        PlayerId = sample.PlayerId.Value,
        DidPlay = sample.DidPlay,
        Statistics = statistics,
        Source = sample.Provenance.Source,
        ExternalId = sample.Provenance.ExternalId,
        FetchedAt = sample.Provenance.FetchedAt,
        SourceTimestamp = sample.Provenance.SourceTimestamp,
        ParserVersion = sample.Provenance.ParserVersion,
        Confidence = sample.Provenance.Confidence,
        RawRecordHash = sample.Provenance.RawRecordHash,
    };
}
