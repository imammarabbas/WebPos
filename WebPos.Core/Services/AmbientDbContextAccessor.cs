using WebPos.Core.Abstractions;
using WebPos.Core.Data;

namespace WebPos.Core.Services;

/// <summary>
/// Async-local ambient DbContext for nested transactional work across services.
/// </summary>
public sealed class AmbientDbContextAccessor : IAmbientDbContextAccessor
{
    private static readonly AsyncLocal<WebPosDbContext?> CurrentContext = new();

    public WebPosDbContext? Current => CurrentContext.Value;

    public WebPosDbContext Required =>
        CurrentContext.Value
        ?? throw new InvalidOperationException(
            "No ambient WebPosDbContext. Call this only inside ITransactionService.ExecuteInTransactionAsync, " +
            "or create a context via IDbContextFactory<WebPosDbContext>.");

    internal static WebPosDbContext? GetAmbient() => CurrentContext.Value;

    internal static void SetAmbient(WebPosDbContext? context) => CurrentContext.Value = context;
}
