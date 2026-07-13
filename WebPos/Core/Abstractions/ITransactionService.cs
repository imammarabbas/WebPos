namespace WebPos.Core.Abstractions;

public interface ITransactionService
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    Task<Guid> PostBalancedEntriesAsync(DoubleEntryPostRequest request, CancellationToken cancellationToken = default);
}
