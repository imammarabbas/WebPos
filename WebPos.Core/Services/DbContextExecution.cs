using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;

namespace WebPos.Core.Services;

/// <summary>
/// Resolves either the ambient transactional context or a short-lived factory context.
/// </summary>
public static class DbContextExecution
{
    public static async Task<T> ExecuteAsync<T>(
        IDbContextFactory<WebPosDbContext> dbFactory,
        Func<WebPosDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(action);

        WebPosDbContext? ambient = AmbientDbContextAccessor.GetAmbient();
        if (ambient is not null)
        {
            return await action(ambient, cancellationToken);
        }

        await using WebPosDbContext context = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await action(context, cancellationToken);
    }

    public static async Task ExecuteAsync(
        IDbContextFactory<WebPosDbContext> dbFactory,
        Func<WebPosDbContext, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            dbFactory,
            async (context, ct) =>
            {
                await action(context, ct);
                return true;
            },
            cancellationToken);
    }
}
