using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Models;

namespace WebPos.Core.Data;

/// <summary>
/// Pessimistic row locks for open cashier shifts (PostgreSQL <c>FOR UPDATE</c>).
/// Must run inside an ambient <see cref="Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"/>.
/// </summary>
public static class CashierShiftLocking
{
    /// <summary>
    /// Locks the shift row by id. Returns a tracked entity, or null if not found.
    /// </summary>
    public static async Task<CashierShift?> LockByIdForUpdateAsync(
        WebPosDbContext context,
        Guid shiftId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Database.IsRelational())
        {
            return await context.CashierShifts.FirstOrDefaultAsync(
                s => s.Id == shiftId && s.TenantId == tenantId,
                cancellationToken);
        }

        try
        {
            return await context.CashierShifts
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM cashier_shifts AS c
                     WHERE c.id = {shiftId}
                       AND c.tenant_id = {tenantId}
                     FOR UPDATE
                     """)
                .IgnoreQueryFilters()
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsLockFailure(ex))
        {
            throw new InsufficientTillBalanceException(
                $"Could not acquire lock on cashier shift {shiftId} for till transfer.",
                shiftId);
        }
    }

    /// <summary>
    /// Locks the OPEN shift for a terminal. Returns a tracked entity, or null if none.
    /// </summary>
    public static async Task<CashierShift?> LockOpenByTerminalForUpdateAsync(
        WebPosDbContext context,
        Guid terminalId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Database.IsRelational())
        {
            return await context.CashierShifts.FirstOrDefaultAsync(
                s => s.TenantId == tenantId
                     && s.TerminalId == terminalId
                     && s.Status == "OPEN",
                cancellationToken);
        }

        try
        {
            return await context.CashierShifts
                .FromSqlInterpolated(
                    $"""
                     SELECT *
                     FROM cashier_shifts AS c
                     WHERE c.terminal_id = {terminalId}
                       AND c.tenant_id = {tenantId}
                       AND c.status = 'OPEN'
                     FOR UPDATE
                     """)
                .IgnoreQueryFilters()
                .AsTracking()
                .SingleOrDefaultAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsLockFailure(ex))
        {
            throw new InsufficientTillBalanceException(
                $"Could not acquire lock on open shift for terminal {terminalId}.");
        }
    }

    private static bool IsLockFailure(Exception ex)
    {
        string full = ex.ToString();
        return ex is TimeoutException
               || full.Contains("lock", StringComparison.OrdinalIgnoreCase)
               || full.Contains("40P01", StringComparison.Ordinal)
               || full.Contains("55P03", StringComparison.Ordinal);
    }
}
