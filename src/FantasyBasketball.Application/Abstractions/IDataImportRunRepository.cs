using FantasyBasketball.Domain.Provenance;

namespace FantasyBasketball.Application.Abstractions;

public interface IDataImportRunRepository
{
    Task AddAsync(DataImportRun run, CancellationToken cancellationToken);

    Task<DataImportRun?> GetAsync(Guid id, CancellationToken cancellationToken);
}
