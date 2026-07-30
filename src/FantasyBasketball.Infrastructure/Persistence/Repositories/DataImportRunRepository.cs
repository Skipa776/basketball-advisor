using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Provenance;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class DataImportRunRepository(FantasyDbContext database)
    : IDataImportRunRepository
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

        return row is null
            ? null
            : new DataImportRun(
                row.Id,
                row.Source,
                Enum.Parse<DataImportRunStatus>(row.Status),
                row.StartedAt,
                row.FinishedAt,
                row.RowsWritten,
                row.PendingIdentityMatches,
                row.FailureDetail);
    }
}
