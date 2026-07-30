namespace FantasyBasketball.Application.Abstractions;

public interface IImportTransaction
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);
}
