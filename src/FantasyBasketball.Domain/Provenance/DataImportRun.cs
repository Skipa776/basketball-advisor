namespace FantasyBasketball.Domain.Provenance;

public enum DataImportRunStatus
{
    Running,
    Succeeded,
    Failed,
}

public sealed record DataImportRun
{
    public DataImportRun(
        Guid id,
        string source,
        DataImportRunStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset? finishedAt,
        int rowsWritten,
        int pendingIdentityMatches,
        string? failureDetail)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Data import run id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (!DataSourceName.IsKnown(source))
        {
            throw new ArgumentException("Source is not a canonical data source name.", nameof(source));
        }

        if (startedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("StartedAt must be UTC.", nameof(startedAt));
        }

        if (finishedAt is { Offset: not { Ticks: 0 } })
        {
            throw new ArgumentException("FinishedAt must be UTC.", nameof(finishedAt));
        }

        if (finishedAt < startedAt)
        {
            throw new ArgumentException("FinishedAt cannot precede StartedAt.", nameof(finishedAt));
        }

        if (rowsWritten < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rowsWritten));
        }

        if (pendingIdentityMatches < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pendingIdentityMatches));
        }

        if (status == DataImportRunStatus.Running && finishedAt is not null)
        {
            throw new ArgumentException("A running import cannot have a finish time.", nameof(finishedAt));
        }

        if (status != DataImportRunStatus.Running && finishedAt is null)
        {
            throw new ArgumentException("A completed import requires a finish time.", nameof(finishedAt));
        }

        if (status == DataImportRunStatus.Failed)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(failureDetail);
        }
        else if (failureDetail is not null)
        {
            throw new ArgumentException(
                "Only a failed import can carry failure detail.",
                nameof(failureDetail));
        }

        Id = id;
        Source = source;
        Status = status;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        RowsWritten = rowsWritten;
        PendingIdentityMatches = pendingIdentityMatches;
        FailureDetail = failureDetail;
    }

    public Guid Id { get; }

    public string Source { get; }

    public DataImportRunStatus Status { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? FinishedAt { get; }

    public int RowsWritten { get; }

    public int PendingIdentityMatches { get; }

    public string? FailureDetail { get; }
}
