using FantasyBasketball.Domain.Modeling;

namespace FantasyBasketball.Application.Abstractions;

public interface IModelVersionRepository
{
    Task AddAsync(
        ModelVersion version,
        CancellationToken cancellationToken);

    Task<ModelVersion?> GetActiveAsync(
        string modelName,
        CancellationToken cancellationToken);

    Task ActivateAsync(
        string modelName,
        string version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ModelVersion>> ListAsync(
        string modelName,
        CancellationToken cancellationToken);
}
