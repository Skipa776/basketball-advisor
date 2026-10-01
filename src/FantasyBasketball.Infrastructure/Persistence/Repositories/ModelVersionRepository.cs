using FantasyBasketball.Application.Abstractions;
using FantasyBasketball.Domain.Modeling;
using FantasyBasketball.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FantasyBasketball.Infrastructure.Persistence.Repositories;

public sealed class ModelVersionRepository(FantasyDbContext database) : IModelVersionRepository
{
    public async Task AddAsync(
        ModelVersion version,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(version);
        database.ModelVersions.Add(ModelVersionRow.Create(
            Guid.NewGuid(),
            version.ModelName,
            version.Version,
            version.FittedAt,
            [.. version.TrainSeasonEndYears],
            version.ParametersJson,
            version.MetricsJson,
            version.CardMarkdown,
            isActive: false));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<ModelVersion?> GetActiveAsync(
        string modelName,
        CancellationToken cancellationToken)
    {
        var row = await database.ModelVersions
            .AsNoTracking()
            .Where(version => version.ModelName == modelName && version.IsActive)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task ActivateAsync(
        string modelName,
        string version,
        CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var active = await database.ModelVersions
            .Where(row => row.ModelName == modelName && row.IsActive)
            .ToArrayAsync(cancellationToken);
        foreach (var row in active)
        {
            row.SetActive(false);
        }

        await database.SaveChangesAsync(cancellationToken);

        var target = await database.ModelVersions
            .FirstOrDefaultAsync(
                row => row.ModelName == modelName && row.Version == version,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Model version '{version}' does not exist for model '{modelName}'.");
        target.SetActive(true);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelVersion>> ListAsync(
        string modelName,
        CancellationToken cancellationToken) =>
        (await database.ModelVersions
            .AsNoTracking()
            .Where(version => version.ModelName == modelName)
            .OrderByDescending(version => version.FittedAt)
            .ThenByDescending(version => version.Id)
            .ToArrayAsync(cancellationToken))
        .Select(ToDomain)
        .ToArray();

    private static ModelVersion ToDomain(ModelVersionRow row) => new(
        row.ModelName,
        row.Version,
        row.FittedAt,
        row.TrainSeasonEndYears,
        row.Parameters,
        row.Metrics,
        row.CardMarkdown);
}
