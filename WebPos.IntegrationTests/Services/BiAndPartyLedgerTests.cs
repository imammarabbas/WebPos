using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Services;

[Collection("Postgres")]
public sealed class BiAndPartyLedgerTests
{
    private readonly PostgresFixture _fixture;

    public BiAndPartyLedgerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CompleteSale_ShouldSnapshotUnitCost_ImmuneToLaterBatchCostChange()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        string invoiceNo = $"INV-{Guid.NewGuid():N}";
        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
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
                    Quantity = 2m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        SalesItem item = await scope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoiceNo);
        item.UnitCostPaisa.Should().Be(10_000L);

        ProductBatch batch = await scope.DbContext.ProductBatches
            .SingleAsync(b => b.Id == seed.BatchId);
        batch.CostPricePaisa = 99_000L;
        await scope.DbContext.SaveChangesAsync();

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        IReadOnlyList<ProductProfitSummary> profit =
            await scope.ReportingService.GetGrossProfitByProductAsync(from, to);

        ProductProfitSummary row = profit.Should().ContainSingle(p => p.ProductId == seed.ProductId).Subject;
        row.CostPaisa.Should().Be(20_000L);
        row.RevenuePaisa.Should().Be(30_000L);
        row.GrossProfitPaisa.Should().Be(10_000L);
        row.MarginPercent.Should().Be(33.33m);
    }

    [Fact]
    public async Task CreditSale_PaymentAllocation_ShouldClearOutstandingAndPostDebitCredit()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"Cust-{Guid.NewGuid():N}"[..20],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 1_000_000
        });

        string invoiceNo = $"INV-{Guid.NewGuid():N}";
        long saleAmount = seed.UnitPricePaisa; // qty 1
        await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = customer.Id,
            PaymentMethod = "CREDIT",
            DiscountAmountPaisa = 0L,
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
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        SalesInvoice invoice = await scope.DbContext.SalesInvoices
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoiceNo);
        invoice.AmountPaidPaisa.Should().Be(0);

        IReadOnlyList<OpenPartySlipDto> open =
            await scope.PartyLedgerService.ListOpenSlipsAsync(customer.Id);
        open.Should().ContainSingle(s => s.InvoiceNo == invoiceNo && s.OutstandingPaisa == saleAmount);

        RecordCustomerPaymentResult payment = await scope.ProcurementService.RecordCustomerPaymentAsync(
            new RecordCustomerPaymentRequest
            {
                CustomerId = customer.Id,
                AmountPaisa = saleAmount,
                PaymentMethod = "CASH",
                ReferenceNo = $"PAY-{Guid.NewGuid():N}"[..20],
                Allocations =
                [
                    new PaymentAllocationRequest
                    {
                        InvoiceNo = invoiceNo,
                        AmountPaisa = saleAmount
                    }
                ]
            });

        payment.PartyLedgerId.Should().NotBe(Guid.Empty);

        invoice = await scope.DbContext.SalesInvoices
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoiceNo);
        invoice.AmountPaidPaisa.Should().Be(saleAmount);

        (await scope.PartyLedgerService.ListOpenSlipsAsync(customer.Id)).Should().BeEmpty();

        PartyPaymentAllocation allocation = await scope.DbContext.PartyPaymentAllocations
            .AsNoTracking()
            .SingleAsync(a => a.PartyLedgerId == payment.PartyLedgerId);
        allocation.InvoiceNo.Should().Be(invoiceNo);
        allocation.AmountPaisa.Should().Be(saleAmount);

        DateTime utcNow = DateTime.UtcNow;
        PartyLedgerSlipDto slip = await scope.PartyLedgerService.GetSlipAsync(
            customer.Id,
            utcNow.Year,
            utcNow.Month);

        slip.Entries.Should().Contain(e =>
            e.ReferenceType == PartyLedgerReferenceTypes.Sale && e.DebitPaisa == saleAmount);
        slip.Entries.Should().Contain(e =>
            e.ReferenceType == PartyLedgerReferenceTypes.PaymentIn && e.CreditPaisa == saleAmount);
        slip.ClosingBalancePaisa.Should().Be(0);
    }
}
