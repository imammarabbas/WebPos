using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Seeds the minimum graph required to complete a cash sale against a real database.
/// </summary>
public static class SeedHelper
{
    public const decimal InitialBatchQty = 10m;
    public const long RetailPricePaisa = 15_000L;

    public static async Task<SaleSeedData> SeedSalePrerequisitesAsync(
        WebPosDbContext context,
        CancellationToken cancellationToken = default,
        decimal? stockQty = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        decimal qty = stockQty is > 0 ? stockQty.Value : InitialBatchQty;
        Guid tenantId = TenantDefaults.MasterTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid roleId = Guid.NewGuid();
        Guid cashierId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        Guid shiftId = Guid.NewGuid();
        Guid supplierId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid batchId = Guid.NewGuid();
        string batchNumber = $"BATCH-{batchId:N}"[..20];
        string productName = "Integration Test Product";
        string barcode = $"BC-{productId:N}"[..20];
        // Prefer stable "1001" for SalesControllerTests; fall back when already taken
        // (shared-DB suite seeds would otherwise hit IX_products_tenant_id_short_code).
        string shortCode = "1001";
        if (await context.Products.AnyAsync(
                p => p.TenantId == tenantId && p.ShortCode == shortCode && !p.IsDeleted,
                cancellationToken))
        {
            shortCode = $"{Random.Shared.Next(2000, 9999)}";
        }

        if (!await context.Tenants.AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = "master",
                IsActive = true,
                CreatedAt = now
            });
        }

        Role? existingRole = await context.Roles.FirstOrDefaultAsync(
            role => role.RoleName == "Cashier" && role.TenantId == tenantId,
            cancellationToken);
        roleId = existingRole?.Id ?? roleId;
        if (existingRole is null)
        {
            context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = "Cashier",
                CreatedAt = now
            });
        }

        context.Users.Add(new User
        {
            Id = cashierId,
            TenantId = tenantId,
            Username = $"CASHIER-{cashierId:N}"[..40],
            PasswordHash = "test-hash",
            RoleId = roleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.Terminals.Add(new Terminal
        {
            Id = terminalId,
            TenantId = tenantId,
            TerminalName = $"TERMINAL-{terminalId:N}"[..40],
            MacAddress = $"MAC-{terminalId:N}"[..20],
            IsActive = true,
            LastSyncTime = now
        });

        context.CashierShifts.Add(new CashierShift
        {
            Id = shiftId,
            TenantId = tenantId,
            TerminalId = terminalId,
            CashierId = cashierId,
            OpenedAt = now,
            OpeningCashPaisa = 0,
            ExpectedCashPaisa = 0,
            DiscrepancyPaisa = 0,
            Status = "OPEN"
        });

        context.Parties.Add(new Party
        {
            Id = supplierId,
            TenantId = tenantId,
            PartyType = "SUPPLIER",
            Name = $"SUPPLIER-{supplierId:N}"[..40],
            PhoneNumber = $"03{supplierId:N}"[..11],
            Address = "Test Address",
            CreditLimitPaisa = 0,
            CurrentBalancePaisa = 0,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = productName,
            Sku = $"SKU-{productId:N}"[..20],
            Barcode = barcode,
            ShortCode = shortCode,
            Brand = "TestBrand",
            BaseUnit = "PCS",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            StockQty = qty,
            CostPricePaisa = 10_000L,
            RetailPricePaisa = RetailPricePaisa,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.ProductBatches.Add(new ProductBatch
        {
            Id = batchId,
            TenantId = tenantId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ExpiryDate = DateOnly.FromDateTime(now.UtcDateTime.AddYears(1)),
            CostPricePaisa = 10_000L,
            RetailPricePaisa = RetailPricePaisa,
            InitialQty = qty,
            CurrentQty = qty,
            SupplierId = supplierId,
            RackLocation = "A1",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return new SaleSeedData(
            TenantId: tenantId,
            CashierId: cashierId,
            TerminalId: terminalId,
            ShiftId: shiftId,
            SupplierId: supplierId,
            ProductId: productId,
            ProductName: productName,
            Barcode: barcode,
            ShortCode: shortCode,
            BatchId: batchId,
            BatchNumber: batchNumber,
            InitialQty: qty,
            UnitPricePaisa: RetailPricePaisa);
    }
}

public sealed record SaleSeedData(
    Guid TenantId,
    Guid CashierId,
    Guid TerminalId,
    Guid ShiftId,
    Guid SupplierId,
    Guid ProductId,
    string ProductName,
    string Barcode,
    string ShortCode,
    Guid BatchId,
    string BatchNumber,
    decimal InitialQty,
    long UnitPricePaisa);
