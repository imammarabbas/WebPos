using WebPos.Core.Data;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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

        context.Roles.Add(new Role
        {
            Id = roleId,
            RoleName = $"CASHIER-{roleId:N}"[..40],
            CreatedAt = now
        });

        context.Users.Add(new User
        {
            Id = cashierId,
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
            TerminalName = $"TERMINAL-{terminalId:N}"[..40],
            MacAddress = $"MAC-{terminalId:N}"[..20],
            IsActive = true,
            LastSyncTime = now
        });

        context.CashierShifts.Add(new CashierShift
        {
            Id = shiftId,
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
            Name = productName,
            Sku = $"SKU-{productId:N}"[..20],
            Barcode = $"BC-{productId:N}"[..20],
            Brand = "TestBrand",
            BaseUnit = "PCS",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.ProductBatches.Add(new ProductBatch
        {
            Id = batchId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ExpiryDate = DateOnly.FromDateTime(now.UtcDateTime.AddYears(1)),
            CostPricePaisa = 10_000L,
            RetailPricePaisa = RetailPricePaisa,
            InitialQty = InitialBatchQty,
            CurrentQty = InitialBatchQty,
            SupplierId = supplierId,
            RackLocation = "A1",
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        return new SaleSeedData(
            CashierId: cashierId,
            TerminalId: terminalId,
            ShiftId: shiftId,
            SupplierId: supplierId,
            ProductId: productId,
            ProductName: productName,
            BatchId: batchId,
            BatchNumber: batchNumber,
            InitialQty: InitialBatchQty,
            UnitPricePaisa: RetailPricePaisa);
    }
}

public sealed record SaleSeedData(
    Guid CashierId,
    Guid TerminalId,
    Guid ShiftId,
    Guid SupplierId,
    Guid ProductId,
    string ProductName,
    Guid BatchId,
    string BatchNumber,
    decimal InitialQty,
    long UnitPricePaisa);
