using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

internal static class FailedRun
{
    public static DataImportRun Create(
        Guid? runId,
        string source,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        Exception exception) =>
        new(
            runId ?? Guid.NewGuid(),
            source,
            DataImportRunStatus.Failed,
            startedAt,
            finishedAt,
            0,
            0,
            $"{exception.GetType().Name}: source import failed.");
}
