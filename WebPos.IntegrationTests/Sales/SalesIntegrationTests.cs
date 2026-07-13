using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Sales;

[Collection("Postgres")]
public sealed class SalesIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public SalesIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteSale_Should_RecordAtomicLedgerEntries()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        const decimal soldQty = 2m;
        long unitPricePaisa = seed.UnitPricePaisa;
        long expectedSaleAmountPaisa = (long)Math.Round(soldQty * unitPricePaisa, MidpointRounding.AwayFromZero);
        string invoiceNo = $"INV-{Guid.NewGuid():N}";

        CompleteSaleRequest request = new()
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = soldQty,
                    UnitPricePaisa = unitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };

        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(request);

        result.TotalAmountPaisa.Should().Be(expectedSaleAmountPaisa);
        result.GrossAmountPaisa.Should().Be(expectedSaleAmountPaisa);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        SalesInvoice? invoice = await assertScope.DbContext.SalesInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);

        invoice.Should().NotBeNull();
        invoice!.TotalAmountPaisa.Should().Be(expectedSaleAmountPaisa);

        List<GeneralLedgerEntry> ledgerEntries = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();

        ledgerEntries.Should().NotBeEmpty();
        long totalDebits = ledgerEntries.Sum(e => e.DebitPaisa);
        long totalCredits = ledgerEntries.Sum(e => e.CreditPaisa);
        totalDebits.Should().Be(totalCredits);

        ProductBatch? batch = await assertScope.DbContext.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == seed.BatchId);

        batch.Should().NotBeNull();
        batch!.CurrentQty.Should().Be(seed.InitialQty - soldQty);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);

        shift.Should().NotBeNull();
        shift!.ExpectedCashPaisa.Should().Be(expectedSaleAmountPaisa);
    }

    [Fact]
    public async Task CompleteSale_Should_Fail_When_InsufficientStock()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        const decimal requestedQty = 99m;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";

        CompleteSaleRequest request = new()
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = requestedQty,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };

        Func<Task> act = () => scope.SalesService.CompleteSaleAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient stock*");

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        SalesInvoice? invoice = await assertScope.DbContext.SalesInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);
        invoice.Should().BeNull();

        bool ledgerExists = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .AnyAsync(e => e.ReferenceNo == invoiceNo);
        ledgerExists.Should().BeFalse();

        ProductBatch? batch = await assertScope.DbContext.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == seed.BatchId);
        batch.Should().NotBeNull();
        batch!.CurrentQty.Should().Be(seed.InitialQty);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift.Should().NotBeNull();
        shift!.ExpectedCashPaisa.Should().Be(0);
    }

    [Fact]
    public async Task ReturnItems_Should_ReverseLedgerAndRestoreInventory()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        const decimal soldQty = 2m;
        const decimal returnQty = 1m;
        long unitPricePaisa = seed.UnitPricePaisa;
        long saleAmountPaisa = (long)Math.Round(soldQty * unitPricePaisa, MidpointRounding.AwayFromZero);
        long refundAmountPaisa = (long)Math.Round(returnQty * unitPricePaisa, MidpointRounding.AwayFromZero);
        string invoiceNo = $"INV-{Guid.NewGuid():N}";

        CompleteSaleResult saleResult = await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = soldQty,
                    UnitPricePaisa = unitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        ReturnItemsResult returnResult = await scope.SalesReturnService.ReturnItemsAsync(new ReturnItemsRequest
        {
            OriginalInvoiceNo = invoiceNo,
            CashierId = seed.CashierId,
            Items =
            [
                new ReturnLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    Quantity = returnQty,
                    ReturnCondition = "GOOD"
                }
            ]
        });

        returnResult.TotalRefundPaisa.Should().Be(refundAmountPaisa);
        returnResult.TransactionGroupId.Should().Be(saleResult.TransactionGroupId);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        ProductBatch? batch = await assertScope.DbContext.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == seed.BatchId);
        batch.Should().NotBeNull();
        batch!.CurrentQty.Should().Be(seed.InitialQty - soldQty + returnQty); // 10 - 2 + 1 = 9

        List<GeneralLedgerEntry> groupEntries = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == saleResult.TransactionGroupId)
            .ToListAsync();

        groupEntries.Should().Contain(e => e.TransactionType == "SALE");
        groupEntries.Should().Contain(e => e.TransactionType == "REVERSAL");

        long totalDebits = groupEntries.Sum(e => e.DebitPaisa);
        long totalCredits = groupEntries.Sum(e => e.CreditPaisa);
        totalDebits.Should().Be(totalCredits);

        // Returned portion nets out: remaining net exposure equals sale - refund.
        long cashDebit = groupEntries.Where(e => e.AccountCode == "CASH").Sum(e => e.DebitPaisa);
        long cashCredit = groupEntries.Where(e => e.AccountCode == "CASH").Sum(e => e.CreditPaisa);
        (cashDebit - cashCredit).Should().Be(saleAmountPaisa - refundAmountPaisa);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift.Should().NotBeNull();
        shift!.ExpectedCashPaisa.Should().Be(saleAmountPaisa - refundAmountPaisa);
    }
}
