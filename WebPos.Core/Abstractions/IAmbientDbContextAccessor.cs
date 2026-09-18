using WebPos.Core.Data;

namespace WebPos.Core.Abstractions;

/// <summary>
/// Provides the ambient <see cref="WebPosDbContext"/> for the current async flow
/// when an <see cref="ITransactionService"/> transaction is active.
/// </summary>
public interface IAmbientDbContextAccessor
{
    WebPosDbContext? Current { get; }

    WebPosDbContext Required { get; }
}
