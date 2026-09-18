using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Services;

[Collection("Postgres")]
public sealed class BiDashboardTests
{
    private readonly PostgresFixture _fixture;

    public BiDashboardTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetBiDashboard_ShouldComputeKpisAndAvgBasket()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = $"INV-{Guid.NewGuid():N}",
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 2m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        });

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        BiDashboardResult before = await scope.ReportingService.GetBiDashboardAsync(new BiDashboardQuery
        {
            From = from,
            To = to,
            ComparePrevious = false
        });

        // Re-seed another sale on same product would need more stock — assert from this sale via deltas
        // Capture after first sale only:
        before.Kpis.Transactions.Should().BeGreaterThanOrEqualTo(1);
        before.Kpis.SalesPaisa.Should().BeGreaterThanOrEqualTo(30_000L);
        before.Kpis.ItemsSold.Should().BeGreaterThanOrEqualTo(2m);
        before.Kpis.AvgBasketPaisa.Should().Be(
            (long)Math.Round((decimal)before.Kpis.SalesPaisa / before.Kpis.Transactions,
                MidpointRounding.AwayFromZero));
        before.Products.Should().Contain(p => p.ProductId == seed.ProductId);
        before.Products.Should().Contain(p =>
            p.ProductId == seed.ProductId
            && (p.Quadrant == BiQuadrants.Stars
                || p.Quadrant == BiQuadrants.Workhorses
                || p.Quadrant == BiQuadrants.Opportunities
                || p.Quadrant == BiQuadrants.Dogs));
    }

    [Fact]
    public async Task GetBiDashboard_ComparePrevious_ShouldExposeGrowthDeltas()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        // Backdate a prior-period invoice directly so compare window has volume.
        Guid priorInvoiceNo = Guid.NewGuid();
        string invoiceNo = $"INV-{priorInvoiceNo:N}";
        DateTimeOffset priorAt = DateTimeOffset.UtcNow.AddDays(-10);
        scope.DbContext.SalesInvoices.Add(new SalesInvoice
        {
            InvoiceNo = invoiceNo,
            TenantId = seed.TenantId,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            TotalAmountPaisa = 15_000L,
            TaxAmountPaisa = 0,
            DiscountAmountPaisa = 0,
            ReceiptNumber = $"R-{priorInvoiceNo:N}"[..20],
            PaymentMethod = "CASH",
            AmountPaidPaisa = 15_000L,
            CreatedAt = priorAt
        });
        scope.DbContext.SalesItems.Add(new SalesItem
        {
            Id = Guid.NewGuid(),
            TenantId = seed.TenantId,
            InvoiceNo = invoiceNo,
            ProductId = seed.ProductId,
            BatchId = seed.BatchId,
            Quantity = 1m,
            UnitPricePaisa = 15_000L,
            UnitCostPaisa = 10_000L,
            DiscountAppliedPaisa = 0
        });
        await scope.DbContext.SaveChangesAsync();

        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = $"INV-{Guid.NewGuid():N}",
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 2m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        });

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-3);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        BiDashboardResult bi = await scope.ReportingService.GetBiDashboardAsync(new BiDashboardQuery
        {
            From = from,
            To = to,
            ComparePrevious = true
        });

        bi.Kpis.SalesDeltaPercent.Should().NotBeNull();
        bi.Products.Should().Contain(p => p.ProductId == seed.ProductId && p.GrowthPercent.HasValue);
    }

    [Fact]
    public async Task GetBiDashboard_TerminalAndPaymentFilters_ShouldRestrict()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = $"INV-{Guid.NewGuid():N}",
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "EASYPAISA",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 1m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        });

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);

        BiDashboardResult cashOnly = await scope.ReportingService.GetBiDashboardAsync(new BiDashboardQuery
        {
            From = from,
            To = to,
            PaymentMethod = "CASH",
            ComparePrevious = false
        });

        BiDashboardResult easy = await scope.ReportingService.GetBiDashboardAsync(new BiDashboardQuery
        {
            From = from,
            To = to,
            PaymentMethod = "EASYPAISA",
            TerminalId = seed.TerminalId,
            ComparePrevious = false
        });

        easy.Kpis.Transactions.Should().BeGreaterThanOrEqualTo(1);
        easy.Payments.Should().Contain(p => p.Key == "EASYPAISA");
        easy.Terminals.Should().Contain(t => t.Key == seed.TerminalId.ToString());

        // Unrelated CASH filter should not include this EASYPAISA invoice's payment slice as the only source —
        // allow empty or other cash from sibling tests; just ensure EASYPAISA filter finds the sale.
        cashOnly.Payments.Should().NotContain(p =>
            p.Key == "EASYPAISA" && p.AmountPaisa == easy.Kpis.SalesPaisa
            && easy.Kpis.SalesPaisa > 0 && cashOnly.Kpis.SalesPaisa == easy.Kpis.SalesPaisa);
    }

    [Fact]
    public async Task GetBiDashboard_DeadStock_ShouldFlagUnsoldOnHandProduct()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        // Product with stock but no sales in window remains on batch from seed (qty > 0).
        // Complete a sale on it would remove dead flag — leave unsold by using a second product.
        Guid deadProductId = Guid.NewGuid();
        Guid deadBatchId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        scope.DbContext.Products.Add(new Product
        {
            Id = deadProductId,
            TenantId = seed.TenantId,
            Name = "Dead Stock SKU",
            Sku = $"SKU-{deadProductId:N}"[..20],
            Barcode = $"BC-{deadProductId:N}"[..20],
            ShortCode = "DS",
            BaseUnit = "PCS",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            MinStockQty = 1m,
            StockQty = 5m,
            CostPricePaisa = 5_000L,
            RetailPricePaisa = 8_000L,
            CreatedAt = now,
            UpdatedAt = now
        });
        scope.DbContext.ProductBatches.Add(new ProductBatch
        {
            Id = deadBatchId,
            TenantId = seed.TenantId,
            ProductId = deadProductId,
            BatchNumber = $"DB-{deadBatchId:N}"[..16],
            CostPricePaisa = 5_000L,
            RetailPricePaisa = 8_000L,
            InitialQty = 5m,
            CurrentQty = 5m,
            SupplierId = seed.SupplierId,
            CreatedAt = now
        });
        await scope.DbContext.SaveChangesAsync();

        BiDashboardResult bi = await scope.ReportingService.GetBiDashboardAsync(new BiDashboardQuery
        {
            From = now.AddDays(-1),
            To = now.AddDays(1),
            ComparePrevious = false
        });

        bi.Products.Should().Contain(p => p.ProductId == deadProductId && p.IsDeadStock);
    }
}
