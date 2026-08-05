using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Services;

[Collection("Postgres")]
public sealed class PartyFinanceSuiteTests
{
    private readonly PostgresFixture _fixture;

    public PartyFinanceSuiteTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PartyFinanceKpis_ReceivableAndTodayCollection_ShouldTrackBalancesAndPayments()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyFinanceKpisDto before = await scope.PartyLedgerService.GetPartyFinanceKpisAsync(
            PartyTypes.Customer);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"KPI-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 2_000_000
        });

        long saleAmount = seed.UnitPricePaisa;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";
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

        PartyFinanceKpisDto afterSale = await scope.PartyLedgerService.GetPartyFinanceKpisAsync(
            PartyTypes.Customer);
        (afterSale.TotalReceivablePaisa - before.TotalReceivablePaisa).Should().Be(saleAmount);

        long payAmount = saleAmount / 2;
        await scope.ProcurementService.RecordCustomerPaymentAsync(new RecordCustomerPaymentRequest
        {
            CustomerId = customer.Id,
            AmountPaisa = payAmount,
            PaymentMethod = "BANK_TRANSFER",
            ReferenceNo = $"PAY-{Guid.NewGuid():N}"[..20]
        });

        PartyFinanceKpisDto afterPay = await scope.PartyLedgerService.GetPartyFinanceKpisAsync(
            PartyTypes.Customer);
        (afterPay.TodayCollectionPaisa - before.TodayCollectionPaisa).Should().Be(payAmount);
        (afterPay.TotalReceivablePaisa - before.TotalReceivablePaisa).Should().Be(saleAmount - payAmount);

        IReadOnlyList<RecentPartyPaymentDto> recent =
            await scope.PartyLedgerService.ListRecentPaymentsAsync(PartyTypes.Customer);
        recent.Should().Contain(r => r.PartyId == customer.Id && r.AmountPaisa == payAmount);
    }

    [Fact]
    public async Task CashPayments_WithOpenShift_ShouldAdjustExpectedCashForCustomerAndSupplier()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        CashierShift shift = await scope.DbContext.CashierShifts
            .SingleAsync(s => s.Id == seed.ShiftId);
        long expectedBefore = shift.ExpectedCashPaisa;

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"CashC-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 500_000
        });

        const long customerPay = 5_000L;
        await scope.ProcurementService.RecordCustomerPaymentAsync(new RecordCustomerPaymentRequest
        {
            CustomerId = customer.Id,
            AmountPaisa = customerPay,
            PaymentMethod = "CASH",
            ReferenceNo = $"ADV-{Guid.NewGuid():N}"[..20],
            ShiftId = seed.ShiftId
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expectedBefore + customerPay);

        PartyDto supplier = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Supplier,
            Name = $"CashS-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 0
        });

        const long supplierPay = 2_500L;
        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = supplierPay,
            PaymentMethod = "CASH",
            ReferenceNo = $"SPAY-{Guid.NewGuid():N}"[..20],
            ShiftId = seed.ShiftId
        });

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(expectedBefore + customerPay - supplierPay);
    }

    [Fact]
    public async Task GetAgeing_OpenSlipOlderThan30Days_ShouldLandIn31To60Bucket()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"Age-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 1_000_000
        });

        long saleAmount = seed.UnitPricePaisa;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";
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
            .SingleAsync(i => i.InvoiceNo == invoiceNo);
        invoice.CreatedAt = DateTimeOffset.UtcNow.AddDays(-45);
        await scope.DbContext.SaveChangesAsync();

        PartyAgeingDto ageing = await scope.PartyLedgerService.GetAgeingAsync(customer.Id);
        ageing.Bucket31To60Paisa.Should().Be(saleAmount);
        ageing.Bucket0To30Paisa.Should().Be(0);
        ageing.Bucket61To90Paisa.Should().Be(0);
        ageing.Bucket90PlusPaisa.Should().Be(0);
    }

    [Fact]
    public async Task GetLedgerDashboard_ShouldSummarizeOpeningSalesPaidAndClosing()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"Dash-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 1_000_000
        });

        long saleAmount = seed.UnitPricePaisa;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";
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

        long payAmount = saleAmount / 2;
        await scope.ProcurementService.RecordCustomerPaymentAsync(new RecordCustomerPaymentRequest
        {
            CustomerId = customer.Id,
            AmountPaisa = payAmount,
            PaymentMethod = "BANK_TRANSFER",
            ReferenceNo = $"PAY-{Guid.NewGuid():N}"[..20]
        });

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        PartyLedgerDashboardDto dash = await scope.PartyLedgerService.GetLedgerDashboardAsync(
            customer.Id,
            from,
            to);

        dash.SalesOrPurchasesPaisa.Should().Be(saleAmount);
        dash.PaidPaisa.Should().Be(payAmount);
        dash.ClosingBalancePaisa.Should().Be(saleAmount - payAmount);
        dash.Entries.Should().NotBeEmpty();
    }
}
