using FantasyBasketball.Application.Abstractions;

namespace FantasyBasketball.Infrastructure.Persistence;

public sealed class EfImportTransaction(FantasyDbContext database) : IImportTransaction
{
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        await using var transaction = await database.Database.BeginTransactionAsync(
            cancellationToken);

        database.DeferAppends = true;
        try
        {
            await action(cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            database.DeferAppends = false;
        }
    }
}
