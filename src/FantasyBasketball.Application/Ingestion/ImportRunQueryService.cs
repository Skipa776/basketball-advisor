using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Application.Players;
using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Ingestion;

public sealed class ImportRunQueryService(
    IImportRunQueryRepository runs,
    IImportJobQueue jobs)
{
    public Task<PagedResult<DataImportRun>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        Paging.Validate(page, limit);
        return ListIncludingActiveAsync(page, limit, cancellationToken);
    }

    private async Task<PagedResult<DataImportRun>> ListIncludingActiveAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var persisted = await runs.ListAsync(page, limit, cancellationToken);
        var active = jobs.GetActive();
        return new PagedResult<DataImportRun>(
            active.Concat(persisted.Items).Take(limit).ToArray(),
            persisted.Total + active.Count,
            page,
            limit);
    }
}
