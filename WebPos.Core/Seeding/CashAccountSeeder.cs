using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Seeding;

/// <summary>
/// Idempotent seed of system cash accounts + one Till account per active terminal.
/// </summary>
public static class CashAccountSeeder
{
    public static async Task SeedAsync(
        WebPosDbContext context,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        List<Guid> tenantIds = await context.Tenants
            .AsNoTracking()
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (Guid tenantId in tenantIds)
        {
            await SeedForTenantAsync(context, tenantId, logger, cancellationToken);
        }
    }

    public static async Task SeedForTenantAsync(
        WebPosDbContext context,
        Guid tenantId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        await EnsureSystemAccountAsync(
            context,
            tenantId,
            name: "Main Bank Account",
            type: CashAccountType.Bank,
            accountCode: LedgerAccounts.Bank,
            paymentMethodKey: "BANK_TRANSFER",
            sortOrder: 10,
            cancellationToken);

        await EnsureSystemAccountAsync(
            context,
            tenantId,
            name: "EasyPaisa Wallet",
            type: CashAccountType.Mobile,
            accountCode: LedgerAccounts.EasyPaisa,
            paymentMethodKey: "EASYPAISA",
            sortOrder: 20,
            cancellationToken);

        await EnsureSystemAccountAsync(
            context,
            tenantId,
            name: "JazzCash Wallet",
            type: CashAccountType.Mobile,
            accountCode: LedgerAccounts.JazzCash,
            paymentMethodKey: "JAZZCASH",
            sortOrder: 30,
            cancellationToken);

        await EnsureSystemAccountAsync(
            context,
            tenantId,
            name: "Petty Cash Drawer",
            type: CashAccountType.Petty,
            accountCode: "PETTY_CASH",
            paymentMethodKey: null,
            sortOrder: 40,
            cancellationToken);

        await EnsureSystemAccountAsync(
            context,
            tenantId,
            name: "Owner / Safe Cash",
            type: CashAccountType.Owner,
            accountCode: "OWNER_CASH",
            paymentMethodKey: null,
            sortOrder: 50,
            cancellationToken);

        List<Terminal> terminals = await context.Terminals
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && t.IsActive)
            .OrderBy(t => t.TerminalName)
            .ToListAsync(cancellationToken);

        int tillSort = 100;
        foreach (Terminal terminal in terminals)
        {
            string accountCode = BuildTillAccountCode(terminal.Id);
            bool exists = await context.CashAccounts
                .IgnoreQueryFilters()
                .AnyAsync(
                    a => a.TenantId == tenantId && a.AccountCode == accountCode,
                    cancellationToken);

            if (exists)
            {
                tillSort += 10;
                continue;
            }

            context.CashAccounts.Add(new CashAccount
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Till - {terminal.TerminalName}",
                Type = CashAccountType.Till,
                AccountCode = accountCode,
                TerminalId = terminal.Id,
                IsActive = true,
                IsSystem = true,
                SortOrder = tillSort,
                PaymentMethodKey = "CASH"
            });

            logger.LogInformation(
                "Seeded till cash account {AccountCode} for terminal {TerminalId}",
                accountCode,
                terminal.Id);
            tillSort += 10;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public static string BuildTillAccountCode(Guid terminalId) =>
        $"CASH:TILL:{terminalId:D}".ToUpperInvariant();

    private static async Task EnsureSystemAccountAsync(
        WebPosDbContext context,
        Guid tenantId,
        string name,
        CashAccountType type,
        string accountCode,
        string? paymentMethodKey,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        bool exists = await context.CashAccounts
            .IgnoreQueryFilters()
            .AnyAsync(
                a => a.TenantId == tenantId && a.AccountCode == accountCode,
                cancellationToken);

        if (exists)
        {
            return;
        }

        context.CashAccounts.Add(new CashAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Type = type,
            AccountCode = accountCode,
            TerminalId = null,
            IsActive = true,
            IsSystem = true,
            SortOrder = sortOrder,
            PaymentMethodKey = paymentMethodKey
        });
    }
}
