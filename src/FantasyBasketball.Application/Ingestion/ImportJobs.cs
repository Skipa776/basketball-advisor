using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public enum ImportJobKind
{
    Players,
    Schedule,
    SeasonStats,
    Adp,
}

public sealed record ImportJobRequest(
    ImportJobKind Kind,
    int? SeasonEndYear = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Csv = null);

public interface IImportJobQueue
{
    Task<DataImportRun> EnqueueAsync(
        ImportJobRequest request,
        CancellationToken cancellationToken);

    IReadOnlyList<DataImportRun> GetActive();
}
