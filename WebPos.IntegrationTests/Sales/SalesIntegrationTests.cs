using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
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

        Product? product = await assertScope.DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seed.ProductId);

        product.Should().NotBeNull();
        product!.StockQty.Should().Be(seed.InitialQty - soldQty);

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

        Product? product = await assertScope.DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seed.ProductId);
        product.Should().NotBeNull();
        product!.StockQty.Should().Be(seed.InitialQty);

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

        Product? product = await assertScope.DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seed.ProductId);
        product.Should().NotBeNull();
        product!.StockQty.Should().Be(seed.InitialQty - soldQty + returnQty); // 10 - 2 + 1 = 9

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
        // Cash sales post to till-specific accounts (CASH:TILL:{terminalId}), not legacy CASH.
        long cashDebit = groupEntries
            .Where(e => LedgerAccounts.IsCashAccount(e.AccountCode))
            .Sum(e => e.DebitPaisa);
        long cashCredit = groupEntries
            .Where(e => LedgerAccounts.IsCashAccount(e.AccountCode))
            .Sum(e => e.CreditPaisa);
        (cashDebit - cashCredit).Should().Be(saleAmountPaisa - refundAmountPaisa);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift.Should().NotBeNull();
        shift!.ExpectedCashPaisa.Should().Be(saleAmountPaisa - refundAmountPaisa);
    }

    [Fact]
    public async Task CompleteSale_OverpayWithCustomerCredit_ShouldPostPaymentAndLowerBalance()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"Overpay-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 5_000_000
        });

        long unitPricePaisa = seed.UnitPricePaisa;
        long netPaisa = unitPricePaisa;
        long excessPaisa = 25_00;
        long amountPaid = netPaisa + excessPaisa;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";

        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = customer.Id,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            AmountPaidPaisa = amountPaid,
            ApplyExcessAsCustomerCredit = true,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 1m,
                    UnitPricePaisa = unitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        result.AmountPaidPaisa.Should().Be(amountPaid);
        result.ChangePaisa.Should().Be(0);
        result.CustomerCreditAppliedPaisa.Should().Be(excessPaisa);
        result.CustomerBalancePaisa.Should().Be(-excessPaisa);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        Party? party = await assertScope.DbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == customer.Id);
        party.Should().NotBeNull();
        party!.CurrentBalancePaisa.Should().Be(-excessPaisa);

        List<PartyLedger> ledger = await assertScope.DbContext.PartyLedgers
            .AsNoTracking()
            .Where(e => e.PartyId == customer.Id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        ledger.Should().Contain(e => e.Type == "SALE" && e.TransactionAmountPaisa == netPaisa);
        ledger.Should().Contain(e =>
            e.Type == "PAYMENT"
            && e.TransactionAmountPaisa == excessPaisa
            && e.ReferenceDetails.Contains("-ADV", StringComparison.Ordinal));

        List<GeneralLedgerEntry> paymentEntries = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.ReferenceNo == $"{invoiceNo}-ADV")
            .ToListAsync();
        paymentEntries.Should().NotBeEmpty();
        paymentEntries.Should().Contain(e =>
            e.TransactionType == "CUSTOMER_PAYMENT"
            && e.AccountCode == LedgerAccounts.AccountsReceivable
            && e.CreditPaisa == excessPaisa);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift.Should().NotBeNull();
        shift!.ExpectedCashPaisa.Should().Be(netPaisa + excessPaisa);
    }

    [Fact]
    public async Task CompleteSale_OverpayWithoutCreditFlag_ShouldReturnChangeOnly()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"Change-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 5_000_000
        });

        long unitPricePaisa = seed.UnitPricePaisa;
        long excessPaisa = 10_00;
        string invoiceNo = $"INV-{Guid.NewGuid():N}";

        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = customer.Id,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            AmountPaidPaisa = unitPricePaisa + excessPaisa,
            ApplyExcessAsCustomerCredit = false,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 1m,
                    UnitPricePaisa = unitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        result.ChangePaisa.Should().Be(excessPaisa);
        result.CustomerCreditAppliedPaisa.Should().Be(0);
        result.CustomerBalancePaisa.Should().Be(0);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();
        Party? party = await assertScope.DbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == customer.Id);
        party!.CurrentBalancePaisa.Should().Be(0);

        bool hasPayment = await assertScope.DbContext.PartyLedgers
            .AsNoTracking()
            .AnyAsync(e => e.PartyId == customer.Id && e.Type == "PAYMENT");
        hasPayment.Should().BeFalse();

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift!.ExpectedCashPaisa.Should().Be(unitPricePaisa);
    }

    [Fact]
    public async Task CompleteSale_WalkIn_NullAmountPaid_ShouldBeFullyPaidLikeExactCash()
    {
        // PaymentModal / F12 path: Walk-In + UI PaymentReceived=0 → AmountPaidPaisa=null
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        const decimal soldQty = 1m;
        long netPaisa = seed.UnitPricePaisa;
        string invoiceNo = $"INV-WI0-{Guid.NewGuid():N}"[..28];

        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = null,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            AmountPaidPaisa = null,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = soldQty,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        });

        result.TotalAmountPaisa.Should().Be(netPaisa);
        result.AmountPaidPaisa.Should().Be(netPaisa);
        result.ChangePaisa.Should().Be(0);
        result.CustomerBalancePaisa.Should().BeNull();

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        SalesInvoice? invoice = await assertScope.DbContext.SalesInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);
        invoice.Should().NotBeNull();
        invoice!.CustomerId.Should().BeNull();
        invoice.AmountPaidPaisa.Should().Be(netPaisa);
        invoice.TotalAmountPaisa.Should().Be(netPaisa);

        List<GeneralLedgerEntry> ledgerEntries = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();

        ledgerEntries.Should().NotBeEmpty();
        ledgerEntries.Where(e => LedgerAccounts.IsCashAccount(e.AccountCode))
            .Should().NotContain(e => e.DebitPaisa == 0 && e.CreditPaisa == 0);
        long cashDebit = ledgerEntries
            .Where(e => LedgerAccounts.IsCashAccount(e.AccountCode))
            .Sum(e => e.DebitPaisa);
        cashDebit.Should().Be(netPaisa);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift!.ExpectedCashPaisa.Should().Be(netPaisa);

        bool anyPartyLedger = await assertScope.DbContext.PartyLedgers
            .AsNoTracking()
            .AnyAsync(e => e.InvoiceNo == invoiceNo);
        anyPartyLedger.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteSale_WalkIn_ExplicitZeroAmountPaid_ShouldFail()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        string invoiceNo = $"INV-WI0F-{Guid.NewGuid():N}"[..28];

        Func<Task> act = () => scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = null,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            AmountPaidPaisa = 0L,
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

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cover the sale total*");
    }

    [Fact]
    public async Task CompleteSale_Registered_ZeroAmountPaid_ShouldLeaveOutstanding()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        PartyDto customer = await scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Customer,
            Name = $"ZeroPay-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 5_000_000
        });

        long netPaisa = seed.UnitPricePaisa;
        string invoiceNo = $"INV-REG0-{Guid.NewGuid():N}"[..28];

        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            CustomerId = customer.Id,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            AmountPaidPaisa = 0L,
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

        result.AmountPaidPaisa.Should().Be(0);
        result.CustomerBalancePaisa.Should().Be(netPaisa);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        SalesInvoice? invoice = await assertScope.DbContext.SalesInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNo == invoiceNo);
        invoice!.AmountPaidPaisa.Should().Be(0);
        invoice.TotalAmountPaisa.Should().Be(netPaisa);

        Party? party = await assertScope.DbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == customer.Id);
        party!.CurrentBalancePaisa.Should().Be(netPaisa);

        List<GeneralLedgerEntry> ledgerEntries = await assertScope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        long cashDebit = ledgerEntries
            .Where(e => LedgerAccounts.IsCashAccount(e.AccountCode))
            .Sum(e => e.DebitPaisa);
        cashDebit.Should().Be(0);

        CashierShift? shift = await assertScope.DbContext.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seed.ShiftId);
        shift!.ExpectedCashPaisa.Should().Be(0);
    }

    [Fact]
    public async Task CompleteSale_LinePriceOverride_PersistsSoldPrice_WithoutMutatingCatalog()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext, stockQty: 20m);

        const long costPaisa = 8_000L;      // Rs 80
        const long catalogPaisa = 10_000L;  // Rs 100
        const long overridePaisa = 9_000L;  // Rs 90
        const long laterCatalogPaisa = 11_000L; // Rs 110

        Product product = await scope.DbContext.Products.SingleAsync(p => p.Id == seed.ProductId);
        product.CostPricePaisa = costPaisa;
        product.RetailPricePaisa = catalogPaisa;
        await scope.DbContext.SaveChangesAsync();

        string invoice1 = $"INV-OV1-{Guid.NewGuid():N}"[..28];
        CompleteSaleResult sale1 = await scope.SalesService.CompleteSaleAsync(
            BuildCashSale(seed, invoice1, quantity: 1m, unitPricePaisa: overridePaisa));

        sale1.TotalAmountPaisa.Should().Be(overridePaisa);

        string invoice2 = $"INV-OV2-{Guid.NewGuid():N}"[..28];
        CompleteSaleResult sale2 = await scope.SalesService.CompleteSaleAsync(
            BuildCashSale(seed, invoice2, quantity: 1m, unitPricePaisa: catalogPaisa));

        sale2.TotalAmountPaisa.Should().Be(catalogPaisa);

        await using IntegrationTestScope assertScope = _fixture.CreateScope();

        Product? catalog = await assertScope.DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seed.ProductId);
        catalog.Should().NotBeNull();
        catalog!.RetailPricePaisa.Should().Be(catalogPaisa);
        catalog.CostPricePaisa.Should().Be(costPaisa);

        SalesItem item1 = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoice1);
        item1.UnitPricePaisa.Should().Be(overridePaisa);
        item1.UnitCostPaisa.Should().Be(costPaisa);
        long profit1 = (item1.UnitPricePaisa * 1) - (item1.UnitCostPaisa * 1);
        profit1.Should().Be(1_000L); // Rs 10

        SalesItem item2 = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoice2);
        item2.UnitPricePaisa.Should().Be(catalogPaisa);
        item2.UnitCostPaisa.Should().Be(costPaisa);
        long profit2 = item2.UnitPricePaisa - item2.UnitCostPaisa;
        profit2.Should().Be(2_000L); // Rs 20

        Product mutable = await assertScope.DbContext.Products.SingleAsync(p => p.Id == seed.ProductId);
        mutable.RetailPricePaisa = laterCatalogPaisa;
        await assertScope.DbContext.SaveChangesAsync();

        SalesItem hist1 = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoice1);
        SalesItem hist2 = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoice2);
        hist1.UnitPricePaisa.Should().Be(overridePaisa);
        (hist1.UnitPricePaisa - hist1.UnitCostPaisa).Should().Be(1_000L);
        hist2.UnitPricePaisa.Should().Be(catalogPaisa);
        (hist2.UnitPricePaisa - hist2.UnitCostPaisa).Should().Be(2_000L);

        string invoice3 = $"INV-OV3-{Guid.NewGuid():N}"[..28];
        CompleteSaleResult sale3 = await assertScope.SalesService.CompleteSaleAsync(
            BuildCashSale(seed, invoice3, quantity: 1m, unitPricePaisa: laterCatalogPaisa));
        sale3.TotalAmountPaisa.Should().Be(laterCatalogPaisa);

        SalesItem item3 = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoice3);
        item3.UnitPricePaisa.Should().Be(laterCatalogPaisa);
        (item3.UnitPricePaisa - item3.UnitCostPaisa).Should().Be(3_000L); // Rs 30
    }

    [Fact]
    public async Task CompleteSale_LinePriceOverride_QuantityMultipliesProfit()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext, stockQty: 20m);

        const long costPaisa = 8_000L;
        const long catalogPaisa = 10_000L;
        const long overridePaisa = 9_000L;
        const decimal qty = 5m;

        Product product = await scope.DbContext.Products.SingleAsync(p => p.Id == seed.ProductId);
        product.CostPricePaisa = costPaisa;
        product.RetailPricePaisa = catalogPaisa;
        await scope.DbContext.SaveChangesAsync();

        string invoiceNo = $"INV-OVQ-{Guid.NewGuid():N}"[..28];
        CompleteSaleResult result = await scope.SalesService.CompleteSaleAsync(
            BuildCashSale(seed, invoiceNo, quantity: qty, unitPricePaisa: overridePaisa));

        result.TotalAmountPaisa.Should().Be(45_000L); // 90 * 5

        await using IntegrationTestScope assertScope = _fixture.CreateScope();
        SalesItem item = await assertScope.DbContext.SalesItems
            .AsNoTracking()
            .SingleAsync(i => i.InvoiceNo == invoiceNo);

        long revenue = (long)Math.Round(item.Quantity * item.UnitPricePaisa, MidpointRounding.AwayFromZero);
        long cogs = (long)Math.Round(item.Quantity * item.UnitCostPaisa, MidpointRounding.AwayFromZero);
        revenue.Should().Be(45_000L);
        cogs.Should().Be(40_000L);
        (revenue - cogs).Should().Be(5_000L); // Rs 50

        Product? catalog = await assertScope.DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == seed.ProductId);
        catalog!.RetailPricePaisa.Should().Be(catalogPaisa);
    }

    [Fact]
    public async Task CompleteSale_Rejects_NonPositive_UnitPrice()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        Func<Task> act = () => scope.SalesService.CompleteSaleAsync(
            BuildCashSale(seed, $"INV-BAD-{Guid.NewGuid():N}"[..28], quantity: 1m, unitPricePaisa: 0));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unit price must be greater than zero*");
    }

    private static CompleteSaleRequest BuildCashSale(
        SaleSeedData seed,
        string invoiceNo,
        decimal quantity,
        long unitPricePaisa) =>
        new()
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
                    Quantity = quantity,
                    UnitPricePaisa = unitPricePaisa,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };
}
