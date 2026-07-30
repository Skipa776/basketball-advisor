namespace FantasyBasketball.Domain.Provenance;

public sealed record DataProvenance
{
    public DataProvenance(
        string source,
        string? externalId,
        DateTimeOffset fetchedAt,
        DateTimeOffset? sourceTimestamp,
        string parserVersion,
        decimal confidence,
        string rawRecordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(parserVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawRecordHash);

        if (!DataSourceName.IsKnown(source))
        {
            throw new ArgumentException("Source is not a canonical data source name.", nameof(source));
        }

        var expectedVersionPrefix = $"{source}-v";
        if (!parserVersion.StartsWith(expectedVersionPrefix, StringComparison.Ordinal)
            || !int.TryParse(
                parserVersion.AsSpan(expectedVersionPrefix.Length),
                out var parserVersionNumber)
            || parserVersionNumber < 1)
        {
            throw new ArgumentException(
                $"ParserVersion must use the format '{source}-v{{n}}'.",
                nameof(parserVersion));
        }

        if (rawRecordHash.Length != 64
            || rawRecordHash.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ArgumentException(
                "RawRecordHash must be a 64-character SHA-256 hexadecimal value.",
                nameof(rawRecordHash));
        }

        if (fetchedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("FetchedAt must be UTC.", nameof(fetchedAt));
        }

        if (sourceTimestamp is { Offset: not { Ticks: 0 } })
        {
            throw new ArgumentException("SourceTimestamp must be UTC.", nameof(sourceTimestamp));
        }

        if (confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        Source = source;
        ExternalId = externalId;
        FetchedAt = fetchedAt;
        SourceTimestamp = sourceTimestamp;
        ParserVersion = parserVersion;
        Confidence = confidence;
        RawRecordHash = rawRecordHash;
    }

    public string Source { get; }

    public string? ExternalId { get; }

    public DateTimeOffset FetchedAt { get; }

    public DateTimeOffset? SourceTimestamp { get; }

    public string ParserVersion { get; }

    public decimal Confidence { get; }

    public string RawRecordHash { get; }
}
