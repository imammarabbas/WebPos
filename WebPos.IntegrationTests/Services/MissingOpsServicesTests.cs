using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.IntegrationTests.Services;

public sealed class MissingOpsServicesTests
{
    [Fact]
    public async Task MilkCollection_ShouldPostInventoryAndSupplierCredit()
    {
        Guid tenantId = TenantDefaults.MasterTenantId;
        await using ServiceHarness harness = await ServiceHarness.CreateAsync(tenantId);
        Guid supplierId = await harness.SeedSupplierAsync();

        RecordMilkCollectionResult result =
            await harness.ProcurementService.RecordMilkCollectionAsync(
                new RecordMilkCollectionRequest
                {
                    SupplierId = supplierId,
                    MilkType = "BUFFALO",
                    LitersReceived = 10m,
                    FatPercent = 6.5m,
                    SnfPercent = 9.1m,
                    RatePerLiterPaisa = 200_00
                });

        result.TotalCreditPaisa.Should().Be(2_000_00);
        DailyMilkCollection collection = await harness.Context.DailyMilkCollections
            .SingleAsync(item => item.Id == result.CollectionId);
        collection.TenantId.Should().Be(tenantId);
        collection.FatPercent.Should().Be(6.5m);
        collection.SnfPercent.Should().Be(9.1m);

        Party supplier = await harness.Context.Parties.SingleAsync(party => party.Id == supplierId);
        supplier.CurrentBalancePaisa.Should().Be(2_000_00);

        long inventoryDebit = await harness.Context.GeneralLedgerEntries
            .Where(entry =>
                entry.TransactionGroupId == result.TransactionGroupId
                && entry.AccountCode == LedgerAccounts.Inventory)
            .SumAsync(entry => entry.DebitPaisa);
        long payableCredit = await harness.Context.GeneralLedgerEntries
            .Where(entry =>
                entry.TransactionGroupId == result.TransactionGroupId
                && entry.AccountCode == LedgerAccounts.AccountsPayable)
            .SumAsync(entry => entry.CreditPaisa);

        inventoryDebit.Should().Be(2_000_00);
        payableCredit.Should().Be(2_000_00);
    }

    [Fact]
    public async Task PurchaseReturn_ShouldReverseInventoryAndSupplierBalance()
    {
        Guid tenantId = TenantDefaults.MasterTenantId;
        await using ServiceHarness harness = await ServiceHarness.CreateAsync(tenantId);
        (Guid managerId, Guid supplierId, Guid productId, Guid batchId, Guid purchaseOrderId) =
            await harness.SeedPurchaseGraphAsync();

        CreatePurchaseReturnResult result =
            await harness.PurchaseReturnService.CreatePurchaseReturnAsync(
                new CreatePurchaseReturnRequest
                {
                    OriginalPurchaseOrderId = purchaseOrderId,
                    ManagerId = managerId,
                    Lines =
                    [
                        new PurchaseReturnLineRequest
                        {
                            ProductId = productId,
                            BatchId = batchId,
                            Quantity = 2m
                        }
                    ]
                });

        result.TotalCreditDeductionPaisa.Should().Be(200_00);
        Product product = await harness.Context.Products
            .SingleAsync(item => item.Id == productId);
        product.StockQty.Should().Be(8m);

        Party supplier = await harness.Context.Parties.SingleAsync(party => party.Id == supplierId);
        supplier.CurrentBalancePaisa.Should().Be(800_00);

        long payableDebit = await harness.Context.GeneralLedgerEntries
            .Where(entry =>
                entry.TransactionGroupId == result.TransactionGroupId
                && entry.AccountCode == LedgerAccounts.AccountsPayable)
            .SumAsync(entry => entry.DebitPaisa);
        payableDebit.Should().Be(200_00);
    }

    [Fact]
    public async Task CloseShift_ShouldLockShiftAndReportCashVariance()
    {
        Guid tenantId = TenantDefaults.MasterTenantId;
        await using ServiceHarness harness = await ServiceHarness.CreateAsync(tenantId);
        (Guid cashierId, Guid terminalId) = await harness.SeedCashierAndTerminalAsync();

        ShiftDto opened = await harness.ShiftService.StartShiftAsync(
            new StartShiftRequest
            {
                CashierId = cashierId,
                TerminalId = terminalId,
                OpeningCashPaisa = 10_000
            });

        CashierShift openShift = await harness.Context.CashierShifts
            .SingleAsync(shift => shift.Id == opened.ShiftId);
        openShift.ExpectedCashPaisa = 12_500;
        await harness.Context.SaveChangesAsync();

        CashVarianceReport report = await harness.ShiftService.CloseShiftAsync(
            new CloseShiftRequest
            {
                ShiftId = opened.ShiftId,
                CashierId = cashierId,
                TerminalId = terminalId,
                ActualCashPaisa = 12_000
            });

        report.Status.Should().Be("CLOSED");
        report.ExpectedCashPaisa.Should().Be(12_500);
        report.ActualCashPaisa.Should().Be(12_000);
        report.DiscrepancyPaisa.Should().Be(-500);
        report.IsBalanced.Should().BeFalse();

        CashierShift closed = await harness.Context.CashierShifts
            .SingleAsync(shift => shift.Id == opened.ShiftId);
        closed.Status.Should().Be("CLOSED");
        closed.ClosedAt.Should().NotBeNull();
        closed.ActualBlindCashPaisa.Should().Be(12_000);

        Func<Task> secondClose = () => harness.ShiftService.CloseShiftAsync(
            new CloseShiftRequest
            {
                ShiftId = opened.ShiftId,
                CashierId = cashierId,
                TerminalId = terminalId,
                ActualCashPaisa = 12_000
            });
        await secondClose.Should().ThrowAsync<ShiftConflictException>();
    }

    private sealed class ServiceHarness : IAsyncDisposable
    {
        private ServiceHarness(
            WebPosDbContext context,
            IProcurementService procurementService,
            IPurchaseReturnService purchaseReturnService,
            IShiftService shiftService,
            Guid tenantId)
        {
            Context = context;
            ProcurementService = procurementService;
            PurchaseReturnService = purchaseReturnService;
            ShiftService = shiftService;
            TenantId = tenantId;
        }

        public WebPosDbContext Context { get; }

        public IProcurementService ProcurementService { get; }

        public IPurchaseReturnService PurchaseReturnService { get; }

        public IShiftService ShiftService { get; }

        public Guid TenantId { get; }

        public static async Task<ServiceHarness> CreateAsync(Guid tenantId)
        {
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase($"WebPos_MissingOps_{Guid.NewGuid():N}")
                    .Options;

            var tenantService = new TestTenantService(tenantId);
            var context = new WebPosDbContext(options, tenantService);
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = $"master-{tenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            IDbContextFactory<WebPosDbContext> dbFactory =
                new TestDbContextFactory(options, tenantService);
            var ambient = new AmbientDbContextAccessor();
            var transactionService = new TransactionService(dbFactory);
            IPartyLedgerService partyLedgerService = new PartyLedgerService(
                dbFactory,
                transactionService,
                tenantService);
            // PartyService is constructed after validators in dedicated Party tests;
            // procurement harness uses a lightweight supplier resolver stub below.
            var partyService = new ProcurementSupplierPartyService(context, tenantService);
            var cashAccountService = new CashAccountService(dbFactory, ambient, tenantService);
            return new ServiceHarness(
                context,
                new ProcurementService(
                    dbFactory,
                    ambient,
                    transactionService,
                    partyLedgerService,
                    partyService,
                    cashAccountService,
                    tenantService),
                new PurchaseReturnService(
                    dbFactory,
                    ambient,
                    transactionService,
                    partyLedgerService,
                    tenantService),
                new ShiftService(dbFactory, ambient, transactionService, tenantService, cashAccountService),
                tenantId);
        }

        public async Task<Guid> SeedSupplierAsync()
        {
            Guid supplierId = Guid.NewGuid();
            Context.Parties.Add(new Party
            {
                Id = supplierId,
                TenantId = TenantId,
                PartyType = "SUPPLIER",
                Name = "Farm Supplier",
                PhoneNumber = Guid.NewGuid().ToString("N")[..16],
                Address = "Farm",
                CreditLimitPaisa = 1_000_000,
                CurrentBalancePaisa = 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await Context.SaveChangesAsync();
            return supplierId;
        }

        public async Task<(Guid CashierId, Guid TerminalId)> SeedCashierAndTerminalAsync()
        {
            Guid roleId = Guid.NewGuid();
            Guid cashierId = Guid.NewGuid();
            Guid terminalId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            Context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TenantId,
                RoleName = "Cashier",
                CreatedAt = now
            });
            Context.Users.Add(new User
            {
                Id = cashierId,
                TenantId = TenantId,
                Username = $"cashier-{cashierId:N}"[..20],
                PasswordHash = "x",
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.Terminals.Add(new Terminal
            {
                Id = terminalId,
                TenantId = TenantId,
                TerminalName = "T1",
                MacAddress = Guid.NewGuid().ToString("N")[..12],
                IsActive = true,
                LastSyncTime = now
            });
            await Context.SaveChangesAsync();
            return (cashierId, terminalId);
        }

        public async Task<(
            Guid ManagerId,
            Guid SupplierId,
            Guid ProductId,
            Guid BatchId,
            Guid PurchaseOrderId)> SeedPurchaseGraphAsync()
        {
            Guid roleId = Guid.NewGuid();
            Guid managerId = Guid.NewGuid();
            Guid supplierId = Guid.NewGuid();
            Guid productId = Guid.NewGuid();
            Guid batchId = Guid.NewGuid();
            Guid purchaseOrderId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            Context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TenantId,
                RoleName = "Admin",
                CreatedAt = now
            });
            Context.Users.Add(new User
            {
                Id = managerId,
                TenantId = TenantId,
                Username = $"mgr-{managerId:N}"[..20],
                PasswordHash = "x",
                RoleId = roleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.Parties.Add(new Party
            {
                Id = supplierId,
                TenantId = TenantId,
                PartyType = "SUPPLIER",
                Name = "PO Supplier",
                PhoneNumber = Guid.NewGuid().ToString("N")[..16],
                Address = "Warehouse",
                CreditLimitPaisa = 5_000_000,
                CurrentBalancePaisa = 1_000_00,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.Products.Add(new Product
            {
                Id = productId,
                TenantId = TenantId,
                Name = "Milk Pack",
                Sku = $"SKU-{productId:N}"[..20],
                Barcode = $"BAR-{productId:N}"[..20],
                Brand = "WebPos",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                StockQty = 10m,
                CostPricePaisa = 100_00,
                RetailPricePaisa = 150_00,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = purchaseOrderId,
                TenantId = TenantId,
                SupplierInvoiceNo = "INV-PO-1",
                SupplierId = supplierId,
                ReceiverId = managerId,
                SubTotalPaisa = 1_000_00,
                DiscountPaisa = 0,
                NetPayablePaisa = 1_000_00,
                PaymentStatus = "CREDIT",
                IsReceived = true,
                CreatedAt = now
            });
            Context.PurchaseItems.Add(new PurchaseItem
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PurchaseOrderId = purchaseOrderId,
                ProductId = productId,
                QuantityReceived = 10m,
                BonusQuantity = 0,
                CostPricePerUnitPaisa = 100_00,
                RetailPricePerUnitPaisa = 150_00,
                BatchNumber = "B-1",
                BatchId = batchId
            });
            Context.ProductBatches.Add(new ProductBatch
            {
                Id = batchId,
                TenantId = TenantId,
                ProductId = productId,
                BatchNumber = "B-1",
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
                CostPricePaisa = 100_00,
                RetailPricePaisa = 150_00,
                InitialQty = 10m,
                CurrentQty = 10m,
                SupplierId = supplierId,
                PurchaseOrderId = purchaseOrderId,
                CreatedAt = now
            });
            await Context.SaveChangesAsync();
            return (managerId, supplierId, productId, batchId, purchaseOrderId);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }

    /// <summary>
    /// Minimal IPartyService used only to enforce SUPPLIER selection in procurement tests.
    /// </summary>
    private sealed class ProcurementSupplierPartyService(
        WebPosDbContext context,
        ITenantService tenantService) : IPartyService
    {
        public Task<WebPos.Core.Abstractions.PartyDto> CreatePartyAsync(
            WebPos.Core.Abstractions.CreatePartyRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WebPos.Core.Abstractions.PartyDto> UpdatePartyAsync(
            Guid partyId,
            WebPos.Core.Abstractions.UpdatePartyRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WebPos.Core.Abstractions.PartyDto>> GetPartiesAsync(
            string? role,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WebPos.Core.Abstractions.PartyDto> GetPartyAsync(
            Guid partyId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async Task<WebPos.Core.Abstractions.PartyDto> GetSupplierAsync(
            Guid supplierId,
            CancellationToken cancellationToken = default)
        {
            Party party = await context.Parties.AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == supplierId
                        && candidate.TenantId == tenantService.TenantId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "Supplier was not found for the current tenant.");

            if (!string.Equals(
                    party.PartyType,
                    "SUPPLIER",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The selected party is not a SUPPLIER.");
            }

            return new WebPos.Core.Abstractions.PartyDto
            {
                Id = party.Id,
                Role = party.PartyType,
                Name = party.Name,
                PhoneNumber = party.PhoneNumber,
                Address = party.Address,
                CreditLimitPaisa = party.CreditLimitPaisa,
                CurrentBalancePaisa = party.CurrentBalancePaisa
            };
        }

        public Task<IReadOnlyList<WebPos.Core.Abstractions.PartyDto>> GetSuppliersAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }
}
