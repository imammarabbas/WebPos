using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Seeding;
using WebPos.Core.Services;
using WebPos.IntegrationTests.Infrastructure;
using DoubleEntryPostRequest = WebPos.Core.Abstractions.DoubleEntryPostRequest;
using LedgerPosting = WebPos.Core.Abstractions.LedgerPosting;

namespace WebPos.IntegrationTests.Services;

[Collection("Postgres")]
public sealed class MasterFinanceSuiteTests
{
    private readonly PostgresFixture _fixture;

    public MasterFinanceSuiteTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetProfitAndLoss_ShouldNetGrossMinusOperatingExpenses_ExcludingDiscount()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset to = DateTimeOffset.UtcNow.AddDays(1);
        // Shared tenant DB may already have rows from sibling tests — assert deltas.
        ProfitAndLossSummary before = await scope.ReportingService.GetProfitAndLossAsync(from, to);

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

        await FundBankAsync(scope, 50_000L);

        // Revenue +30000, cost +20000, GP +10000
        var expenseService = new ExpenseService(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

        await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = null,
            LoggedByUserId = seed.CashierId,
            Description = "Rent July",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"RENT-{Guid.NewGuid():N}",
            PaymentMethod = "BANK_TRANSFER",
            AmountPaisa = 3_000L
        });

        // Discount category must not count as operating overhead for P&L.
        await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = null,
            LoggedByUserId = seed.CashierId,
            Description = "Promo discount bucket",
            ExpenseCategory = ExpenseCategories.Discount,
            ReceiptReference = $"DISC-{Guid.NewGuid():N}",
            PaymentMethod = "BANK_TRANSFER",
            AmountPaisa = 9_999L
        });

        ProfitAndLossSummary pnl = await scope.ReportingService.GetProfitAndLossAsync(from, to);

        (pnl.RevenuePaisa - before.RevenuePaisa).Should().Be(30_000L);
        (pnl.CostPaisa - before.CostPaisa).Should().Be(20_000L);
        (pnl.GrossProfitPaisa - before.GrossProfitPaisa).Should().Be(10_000L);
        (pnl.OperatingExpensesPaisa - before.OperatingExpensesPaisa).Should().Be(3_000L);
        (pnl.NetProfitPaisa - before.NetProfitPaisa).Should().Be(7_000L);
        pnl.ExpensesByCategory.Should().NotContain(e => e.ExpenseCategory == ExpenseCategories.Discount);
    }

    [Fact]
    public async Task OverheadExpense_NullShift_ShouldPostGlWithoutTouchingDrawer()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        long expectedBefore = await scope.DbContext.CashierShifts
            .Where(s => s.Id == seed.ShiftId)
            .Select(s => s.ExpectedCashPaisa)
            .SingleAsync();

        await FundBankAsync(scope, 50_000L);

        var expenseService = new ExpenseService(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

        RecordExpenseResult result = await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = null,
            LoggedByUserId = seed.CashierId,
            Description = "Electricity bill",
            ExpenseCategory = ExpenseCategories.Electricity,
            ReceiptReference = "ELEC-1",
            PaymentMethod = "BANK_TRANSFER",
            AmountPaisa = 5_000L
        });

        result.VoucherNo.Should().NotBeNullOrWhiteSpace();

        long expectedAfter = await scope.DbContext.CashierShifts
            .AsNoTracking()
            .Where(s => s.Id == seed.ShiftId)
            .Select(s => s.ExpectedCashPaisa)
            .SingleAsync();
        expectedAfter.Should().Be(expectedBefore);

        List<GeneralLedgerEntry> entries = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        entries.Sum(e => e.DebitPaisa).Should().Be(entries.Sum(e => e.CreditPaisa));
        entries.Should().Contain(e =>
            e.AccountCode == LedgerAccounts.Expense(ExpenseCategories.Electricity)
            && e.DebitPaisa == 5_000L);
    }

    [Fact]
    public async Task TillExpense_OpenShiftCash_ShouldReduceExpectedCash()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(
            seed.TerminalId,
            "Pilot Till");

        // Seed some till cash via a sale first.
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
                    Quantity = 1m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        });

        long expectedBefore = await scope.DbContext.CashierShifts
            .Where(s => s.Id == seed.ShiftId)
            .Select(s => s.ExpectedCashPaisa)
            .SingleAsync();

        var expenseService = new ExpenseService(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

        RecordExpenseResult result = await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "Lightbulbs from till",
            ExpenseCategory = ExpenseCategories.Equipment,
            ReceiptReference = "FIX-1",
            PaymentMethod = "CASH",
            CashAccountId = till.Id,
            AmountPaisa = 2_000L
        });

        long expectedAfter = await scope.DbContext.CashierShifts
            .AsNoTracking()
            .Where(s => s.Id == seed.ShiftId)
            .Select(s => s.ExpectedCashPaisa)
            .SingleAsync();
        expectedAfter.Should().Be(expectedBefore - 2_000L);

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e =>
            e.AccountCode == till.AccountCode && e.CreditPaisa == 2_000L);
        legs.Should().NotContain(e =>
            e.AccountCode == LedgerAccounts.Cash && e.CreditPaisa == 2_000L);
    }

    [Fact]
    public async Task CashExpense_WithoutFundingAccount_ShouldReject()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        var expenseService = new ExpenseService(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

        Func<Task> act = () => expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "Missing funding",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = "RENT-X",
            PaymentMethod = "CASH",
            AmountPaisa = 1_000L
        });

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*funding account*");
    }

    private static Task FundBankAsync(IntegrationTestScope scope, long amountPaisa) =>
        scope.TransactionService.ExecuteInTransactionAsync(ct =>
            scope.TransactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "TEST_FUND",
                    ReferenceNo = $"FUND-{Guid.NewGuid():N}"[..16],
                    ReferenceDetails = "Test bank funding",
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Bank,
                            DebitPaisa = amountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Revenue,
                            DebitPaisa = 0,
                            CreditPaisa = amountPaisa
                        }
                    ]
                },
                ct));
}
