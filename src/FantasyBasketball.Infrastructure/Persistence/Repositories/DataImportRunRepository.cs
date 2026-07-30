using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Application.Common;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class DataImportRunRepository(FantasyDbContext database)
    : IDataImportRunRepository, IImportRunQueryRepository
{
    public async Task AddAsync(
        DataImportRun run,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        database.DataImportRuns.Add(DataImportRunRow.Create(
            run.Id,
            run.Source,
            run.Status.ToString(),
            run.StartedAt,
            run.FinishedAt,
            run.RowsWritten,
            run.PendingIdentityMatches,
            run.FailureDetail));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<DataImportRun?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await database.DataImportRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

        return row is null ? null : Map(row);
    }

    public async Task<PagedResult<DataImportRun>> ListAsync(
        int page,
        int limit,
        CancellationToken cancellationToken)
    {
        var total = await database.DataImportRuns.CountAsync(cancellationToken);
        var rows = await database.DataImportRuns
            .AsNoTracking()
            .OrderByDescending(value => value.StartedAt)
            .ThenByDescending(value => value.Id)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return new PagedResult<DataImportRun>(
            rows.Select(Map).ToArray(),
            total,
            page,
            limit);
    }

    public async Task<IReadOnlyList<DataImportRun>> ListRecentAsync(
        CancellationToken cancellationToken) =>
        (await database.DataImportRuns
            .AsNoTracking()
            .OrderByDescending(value => value.StartedAt)
            .Take(500)
            .ToArrayAsync(cancellationToken))
        .Select(Map)
        .ToArray();

    private static DataImportRun Map(DataImportRunRow row) =>
        new(
            row.Id,
            row.Source,
            Enum.Parse<DataImportRunStatus>(row.Status),
            row.StartedAt,
            row.FinishedAt,
            row.RowsWritten,
            row.PendingIdentityMatches,
            row.FailureDetail);
}
