using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;
using WebPos.Core.Validation;

namespace WebPos.IntegrationTests.Services;

public sealed class PurchaseServiceTests
{
    [Fact]
    public async Task CreateAndReceive_ShouldCreateBatchesAndPostInventoryAp()
    {
        await using PurchaseHarness harness = await PurchaseHarness.CreateAsync();
        (Guid receiverId, Guid supplierId, Guid productId) = await harness.SeedCatalogAsync();

        CreatePurchaseOrderResult created = await harness.PurchaseService.CreatePurchaseOrderAsync(
            new CreatePurchaseRequest
            {
                SupplierId = supplierId,
                ReceiverId = receiverId,
                SupplierInvoiceNo = "INV-100",
                PurchaseDate = DateTimeOffset.UtcNow.AddHours(-1),
                DiscountPaisa = 0,
                Lines =
                [
                    new CreatePurchaseLineRequest
                    {
                        ProductId = productId,
                        Quantity = 10m,
                        BonusQuantity = 1m,
                        PurchasePricePaisa = 100_00,
                        RetailPricePaisa = 150_00,
                        BatchNumber = "BATCH-A",
                        ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6))
                    }
                ]
            });

        created.IsReceived.Should().BeFalse();
        created.NetPayablePaisa.Should().Be(1_000_00);

        ReceiveStockResult received = await harness.PurchaseService.ReceiveStockAsync(
            created.PurchaseOrderId);

        received.BatchIds.Should().HaveCount(1);
        received.NetPayablePaisa.Should().Be(1_000_00);

        PurchaseOrder order = await harness.Context.PurchaseOrders
            .SingleAsync(o => o.Id == created.PurchaseOrderId);
        order.IsReceived.Should().BeTrue();
        order.PaymentStatus.Should().Be("CREDIT");
        order.TenantId.Should().Be(harness.TenantId);

        ProductBatch batch = await harness.Context.ProductBatches
            .SingleAsync(b => b.Id == received.BatchIds[0]);
        batch.CurrentQty.Should().Be(11m);
        batch.PurchasePricePaisa.Should().Be(100_00);
        batch.CostPricePaisa.Should().Be(100_00);
        batch.ExpiryDate.Should().NotBeNull();
        batch.TenantId.Should().Be(harness.TenantId);

        List<GeneralLedgerEntry> ledger = await harness.Context.GeneralLedgerEntries
            .Where(e => e.TransactionGroupId == received.TransactionGroupId)
            .ToListAsync();
        ledger.Should().Contain(e =>
            e.AccountCode == LedgerAccounts.Inventory && e.DebitPaisa == 1_000_00);
        ledger.Should().Contain(e =>
            e.AccountCode == LedgerAccounts.AccountsPayable && e.CreditPaisa == 1_000_00);

        PartyLedger partyLedger = await harness.Context.PartyLedgers
            .SingleAsync(e =>
                e.PartyId == supplierId
                && e.Type == "PURCHASE"
                && e.PurchaseOrderId == created.PurchaseOrderId);
        partyLedger.TransactionAmountPaisa.Should().Be(1_000_00);
    }

    [Fact]
    public async Task ReceiveStockAsync_ShouldRejectDoublePost()
    {
        await using PurchaseHarness harness = await PurchaseHarness.CreateAsync();
        (Guid receiverId, Guid supplierId, Guid productId) = await harness.SeedCatalogAsync();

        CreatePurchaseOrderResult created = await harness.PurchaseService.CreatePurchaseOrderAsync(
            new CreatePurchaseRequest
            {
                SupplierId = supplierId,
                ReceiverId = receiverId,
                SupplierInvoiceNo = "INV-200",
                PurchaseDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new CreatePurchaseLineRequest
                    {
                        ProductId = productId,
                        Quantity = 2m,
                        PurchasePricePaisa = 50_00,
                        RetailPricePaisa = 80_00
                    }
                ]
            });

        await harness.PurchaseService.ReceiveStockAsync(created.PurchaseOrderId);

        Func<Task> again = () =>
            harness.PurchaseService.ReceiveStockAsync(created.PurchaseOrderId);

        await again.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Order already processed.");
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectFuturePurchaseDate()
    {
        await using PurchaseHarness harness = await PurchaseHarness.CreateAsync();
        (Guid receiverId, Guid supplierId, Guid productId) = await harness.SeedCatalogAsync();

        Func<Task> act = () => harness.PurchaseService.CreatePurchaseOrderAsync(
            new CreatePurchaseRequest
            {
                SupplierId = supplierId,
                ReceiverId = receiverId,
                SupplierInvoiceNo = "INV-FUTURE",
                PurchaseDate = DateTimeOffset.UtcNow.AddDays(1),
                Lines =
                [
                    new CreatePurchaseLineRequest
                    {
                        ProductId = productId,
                        Quantity = 1m,
                        PurchasePricePaisa = 10_00,
                        RetailPricePaisa = 20_00
                    }
                ]
            });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*PurchaseDate*");
    }

    [Fact]
    public async Task CreatePurchaseOrderAsync_ShouldRejectCustomerAsSupplier()
    {
        await using PurchaseHarness harness = await PurchaseHarness.CreateAsync();
        (Guid receiverId, _, Guid productId) = await harness.SeedCatalogAsync();

        Party customer = new()
        {
            Id = Guid.NewGuid(),
            TenantId = harness.TenantId,
            PartyType = PartyTypes.Customer,
            Name = "Not A Supplier",
            PhoneNumber = Guid.NewGuid().ToString("N")[..16],
            Address = "",
            CreditLimitPaisa = 0,
            CurrentBalancePaisa = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        harness.Context.Parties.Add(customer);
        await harness.Context.SaveChangesAsync();

        Func<Task> act = () => harness.PurchaseService.CreatePurchaseOrderAsync(
            new CreatePurchaseRequest
            {
                SupplierId = customer.Id,
                ReceiverId = receiverId,
                SupplierInvoiceNo = "INV-BAD",
                PurchaseDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new CreatePurchaseLineRequest
                    {
                        ProductId = productId,
                        Quantity = 1m,
                        PurchasePricePaisa = 10_00,
                        RetailPricePaisa = 20_00
                    }
                ]
            });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*SUPPLIER*");
    }

    private sealed class PurchaseHarness : IAsyncDisposable
    {
        private PurchaseHarness(
            WebPosDbContext context,
            IPurchaseService purchaseService,
            Guid tenantId)
        {
            Context = context;
            PurchaseService = purchaseService;
            TenantId = tenantId;
        }

        public WebPosDbContext Context { get; }

        public IPurchaseService PurchaseService { get; }

        public Guid TenantId { get; }

        public static async Task<PurchaseHarness> CreateAsync()
        {
            Guid tenantId = TenantDefaults.MasterTenantId;
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase($"WebPos_Purchase_{Guid.NewGuid():N}")
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

            var transactionService = new TransactionService(context);
            IPartyLedgerService ledgerService = new PartyLedgerService(
                context,
                transactionService,
                tenantService);
            IValidator<CreatePartyRequest> partyValidator =
                new CreatePartyRequestValidator(context, tenantService);
            IPartyService partyService = new PartyService(
                context,
                transactionService,
                ledgerService,
                tenantService,
                partyValidator);
            IValidator<CreatePurchaseRequest> purchaseValidator =
                new CreatePurchaseRequestValidator(context, tenantService);
            IPurchaseService purchaseService = new PurchaseService(
                context,
                transactionService,
                ledgerService,
                partyService,
                tenantService,
                purchaseValidator);

            return new PurchaseHarness(context, purchaseService, tenantId);
        }

        public async Task<(Guid ReceiverId, Guid SupplierId, Guid ProductId)> SeedCatalogAsync()
        {
            Guid roleId = Guid.NewGuid();
            Guid receiverId = Guid.NewGuid();
            Guid supplierId = Guid.NewGuid();
            Guid productId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            Context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TenantId,
                RoleName = "Receiver",
                CreatedAt = now
            });
            Context.Users.Add(new User
            {
                Id = receiverId,
                TenantId = TenantId,
                Username = $"rcv-{receiverId:N}"[..20],
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
                PartyType = PartyTypes.Supplier,
                Name = "Purchase Supplier",
                PhoneNumber = Guid.NewGuid().ToString("N")[..16],
                Address = "Depot",
                CreditLimitPaisa = 5_000_000,
                CurrentBalancePaisa = 0,
                CreatedAt = now,
                UpdatedAt = now
            });
            Context.Products.Add(new Product
            {
                Id = productId,
                TenantId = TenantId,
                Name = "Rice 5kg",
                Sku = $"SKU-{productId:N}"[..20],
                Barcode = $"BAR-{productId:N}"[..20],
                Brand = "WebPos",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                CreatedAt = now,
                UpdatedAt = now
            });
            await Context.SaveChangesAsync();
            return (receiverId, supplierId, productId);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
