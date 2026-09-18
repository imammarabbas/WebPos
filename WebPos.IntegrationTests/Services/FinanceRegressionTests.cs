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
using CashVarianceReport = Common.Models.CashVarianceReport;
using CloseShiftRequest = Common.Models.CloseShiftRequest;
using StartShiftRequest = Common.Models.StartShiftRequest;

namespace WebPos.IntegrationTests.Services;

/// <summary>
/// Explicit 25-case finance regression matrix (Cases 1–25 from finance completion plan).
/// Complements <see cref="CashTreasuryTests"/> with stock/position/reconciliation coverage.
/// </summary>
[Collection("Postgres")]
public sealed class FinanceRegressionTests
{
    private readonly PostgresFixture _fixture;

    public FinanceRegressionTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Case01_CashSale_ShouldPostToTillSpecificGl()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(scope.DbContext, TenantDefaults.MasterTenantId, NullLogger.Instance);
        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(seed.TerminalId, "Till");

        await scope.SalesService.CompleteSaleAsync(BuildSale(seed, qty: 1m));

        long tillGl = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        tillGl.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Case02_UnrecordedPhysical_WithoutBlindCount_DoesNotAutoPostSale()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (_, CashAccountDto tillAccount, _) = await SeedTillAsync(scope, glPaisa: 50_000L, drawerPaisa: 40_000L);

        CashStockReconciliationDto recon =
            await scope.BusinessPositionService.GetCashStockReconciliationAsync();

        TillCashVisibilityDto till = recon.Cash
            .Should().ContainSingle(t => t.TillName == tillAccount.Name)
            .Subject;
        till.HasPhysicalCount.Should().BeFalse();
        till.UnrecordedPhysicalSurplusPaisa.Should().Be(0);

        long unregistered = await scope.CashAccountService.GetAccountBalancePaisaByCodeAsync(
            LedgerAccounts.UnregisteredCash);
        unregistered.Should().Be(0);
    }

    [Fact]
    public async Task Case03_AvailableCash_IsMinOfTillGlAndExpected()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (_, CashAccountDto till, CashierShift shift) = await SeedTillAsync(scope, 100_000L, 60_000L);

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            shift.Id);
        spendable.SpendablePaisa.Should().Be(60_000L);
    }

    [Fact]
    public async Task Case04_TillRentExpense_ReducesTillGlAndExpected()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) = await SeedTillAsync(scope, 100_000L, 100_000L);
        var expenseService = CreateExpenseService(scope);

        await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "Rent",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"R-{Guid.NewGuid():N}"[..12],
            PaymentMethod = "CASH",
            CashAccountId = till.Id,
            AmountPaisa = 20_000L
        });

        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(80_000L);
    }

    [Fact]
    public async Task Case05_CashIn_AlignPath_RaisesAvailableWithoutTillGlDebit()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, 80_000L, 50_000L);

        long glBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 20_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        long glAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        glAfter.Should().Be(glBefore);

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            shift.Id);
        spendable.SpendablePaisa.Should().Be(70_000L);
    }

    [Fact]
    public async Task Case06_OpeningCash_IsNotNewGlMoney()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) = await SeedTillAsync(scope, 100_000L, 100_000L);

        await scope.ShiftService.CloseShiftAsync(new CloseShiftRequest
        {
            ShiftId = seed.ShiftId,
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            ActualCashPaisa = 100_000L
        });

        long glBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        await scope.ShiftService.StartShiftAsync(new StartShiftRequest
        {
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            OpeningCashPaisa = glBefore
        });

        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(glBefore);
    }

    [Fact]
    public async Task Case07_PhysicalBlindCount_NeverOverwritesExpected()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, _, CashierShift shift) = await SeedTillAsync(scope, 50_000L, 40_000L);

        CashVarianceReport report = await scope.ShiftService.CloseShiftAsync(new CloseShiftRequest
        {
            ShiftId = seed.ShiftId,
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            ActualCashPaisa = 35_000L
        });

        report.ExpectedCashPaisa.Should().Be(40_000L);
        report.PhysicalCountPaisa.Should().Be(35_000L);

        CashierShift closed = await scope.DbContext.CashierShifts
            .AsNoTracking()
            .SingleAsync(s => s.Id == seed.ShiftId);
        closed.ExpectedCashPaisa.Should().Be(40_000L);
        closed.ActualBlindCashPaisa.Should().Be(35_000L);
    }

    [Fact]
    public async Task Case08_StockDifference_DoesNotCreateSaleOrCashLink()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);

        long unregisteredBefore = await scope.CashAccountService.GetAccountBalancePaisaByCodeAsync(
            LedgerAccounts.UnregisteredCash);

        await scope.StockPositionService.RecordPhysicalCountAsync(new RecordStockCountRequest
        {
            Lines =
            [
                new RecordStockCountLineRequest
                {
                    ProductId = seed.ProductId,
                    CountedQty = 1m
                }
            ]
        });

        StockPositionSummaryDto stock = await scope.StockPositionService.GetPositionAsync();
        stock.MissingUnexplainedValuePaisa.Should().BeGreaterThan(0);

        long unregisteredAfter = await scope.CashAccountService.GetAccountBalancePaisaByCodeAsync(
            LedgerAccounts.UnregisteredCash);
        unregisteredAfter.Should().Be(unregisteredBefore);
    }

    [Fact]
    public async Task Case09_CashStockReconciliation_DoesNotLinkVariances()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, _, _) = await SeedTillAsync(scope, 50_000L, 40_000L);

        await scope.StockPositionService.RecordPhysicalCountAsync(new RecordStockCountRequest
        {
            Lines = [new RecordStockCountLineRequest { ProductId = seed.ProductId, CountedQty = 1m }]
        });

        CashStockReconciliationDto recon =
            await scope.BusinessPositionService.GetCashStockReconciliationAsync();

        recon.Stock.MissingUnexplainedValuePaisa.Should().BeGreaterThanOrEqualTo(0);
        recon.Cash.Should().NotBeEmpty();
        // No combined "explained by sale" field — variances remain independent.
        recon.Stock.ExpectedValuePaisa.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Case10_StockAsOfDate_ReconstructsFromMovements()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext, stockQty: 10m);
        DateTimeOffset beforeSale = DateTimeOffset.UtcNow;

        StockPositionSummaryDto before = await scope.StockPositionService.GetPositionAsync(beforeSale);
        decimal qtyBefore = before.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty;
        qtyBefore.Should().Be(10m);

        await scope.SalesService.CompleteSaleAsync(BuildSale(seed, qty: 3m));

        StockPositionSummaryDto after = await scope.StockPositionService.GetPositionAsync();
        after.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty.Should().Be(7m);

        StockPositionSummaryDto historical = await scope.StockPositionService.GetPositionAsync(beforeSale);
        historical.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty.Should().Be(10m);
    }

    [Fact]
    public async Task Case11_CashExpense_RequiresFundingAccount()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        var expenseService = CreateExpenseService(scope);

        Func<Task> act = () => expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "No funding",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"X-{Guid.NewGuid():N}"[..12],
            PaymentMethod = "CASH",
            AmountPaisa = 1_000L
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Case12_SupplierPayment_FromTill_HitsTillGl()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) = await SeedTillAsync(scope, 100_000L, 100_000L);
        PartyDto supplier = await CreateSupplierAsync(scope);

        long before = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        await scope.ProcurementService.RecordSupplierPaymentAsync(new RecordSupplierPaymentRequest
        {
            SupplierId = supplier.Id,
            AmountPaisa = 5_000L,
            PaymentMethod = "CASH",
            CashAccountId = till.Id,
            ShiftId = seed.ShiftId,
            ReferenceNo = $"PAY-{Guid.NewGuid():N}"[..12]
        });

        (before - await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(5_000L);
    }

    [Fact]
    public async Task Case13_CashDrop_SemanticsUnchanged()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) =
            await SeedTillAsync(scope, 100_000L, 100_000L);
        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Single(a => a.AccountCode == LedgerAccounts.Bank);

        await scope.CashTransferService.TransferAsync(
            till.Id,
            bank.Id,
            10_000L,
            "Case13 cash drop",
            seed.ShiftId);

        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(90_000L);
    }

    [Fact]
    public async Task Case14_OwnerInvestment_PostsToEquityNotRevenue()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(scope.DbContext, TenantDefaults.MasterTenantId, NullLogger.Instance);
        CashAccountDto owner = (await scope.CashAccountService.ListAsync())
            .Single(a => a.AccountCode == "OWNER_CASH");

        CapitalFundingResult result = await scope.CashTransferService.RecordOwnerInvestmentAsync(
            new CapitalFundingRequest { CashAccountId = owner.Id, AmountPaisa = 10_000L });

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == LedgerAccounts.OwnerCapital);
        legs.Should().NotContain(e => e.AccountCode == LedgerAccounts.Revenue);
    }

    [Fact]
    public async Task Case15_LoanReceived_PostsToLiability()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(scope.DbContext, TenantDefaults.MasterTenantId, NullLogger.Instance);
        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Single(a => a.AccountCode == LedgerAccounts.Bank);

        CapitalFundingResult result = await scope.CashTransferService.RecordLoanReceivedAsync(
            new CapitalFundingRequest { CashAccountId = bank.Id, AmountPaisa = 50_000L });

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionGroupId == result.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == LedgerAccounts.LoanPayable && e.CreditPaisa == 50_000L);
    }

    [Fact]
    public async Task Case16_LoanRepayment_ReducesLiabilityNotExpense()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await CashAccountSeeder.SeedForTenantAsync(scope.DbContext, TenantDefaults.MasterTenantId, NullLogger.Instance);
        CashAccountDto bank = (await scope.CashAccountService.ListAsync())
            .Single(a => a.AccountCode == LedgerAccounts.Bank);

        await scope.CashTransferService.RecordLoanReceivedAsync(
            new CapitalFundingRequest { CashAccountId = bank.Id, AmountPaisa = 50_000L });

        CapitalFundingResult repaid = await scope.CashTransferService.RecordLoanRepaymentAsync(
            new CapitalFundingRequest { CashAccountId = bank.Id, AmountPaisa = 20_000L });

        List<GeneralLedgerEntry> legs = await scope.DbContext.GeneralLedgerEntries.AsNoTracking()
            .Where(e => e.TransactionGroupId == repaid.TransactionGroupId)
            .ToListAsync();
        legs.Should().Contain(e => e.AccountCode == LedgerAccounts.LoanPayable && e.DebitPaisa == 20_000L);
        legs.Should().NotContain(e => e.AccountCode.StartsWith("EXPENSE:"));
    }

    [Fact]
    public async Task Case17_CashIn_WhenLedgerExceedsDrawer_AlignsExpected()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) =
            await SeedTillAsync(scope, 80_000L, 50_000L);

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 15_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        CashierShift shift = await scope.DbContext.CashierShifts
            .AsNoTracking()
            .SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa.Should().Be(65_000L);
        (await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id)).Should().Be(80_000L);
    }

    [Fact]
    public async Task Case18_CashIn_ExcessPath_MayDebitTillGl()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) =
            await SeedTillAsync(scope, 40_000L, 60_000L);

        long glBefore = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 10_000L,
            Reason = ShiftCashInReasons.OwnerCashAdded
        });

        long glAfter = await scope.CashAccountService.GetAccountBalancePaisaAsync(till.Id);
        glAfter.Should().BeGreaterThan(glBefore);
    }

    [Fact]
    public async Task Case19_CashOut_RejectsWhenOverAvailable()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, CashierShift shift) = await SeedTillAsync(scope, 50_000L, 40_000L);

        Func<Task> act = () => scope.CashTransferService.RecordTillCashOutAsync(new RecordTillCashOutRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 50_000L,
            Reason = "Test out"
        });

        await act.Should().ThrowAsync<InsufficientCashBalanceException>();
        await scope.DbContext.Entry(shift).ReloadAsync();
        shift.ExpectedCashPaisa.Should().Be(40_000L);
    }

    [Fact]
    public async Task Case20_CloseShift_LedgerExceedsExpected_NoForcedShortage()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, _, _) = await SeedTillAsync(scope, 80_000L, 50_000L);

        CashVarianceReport closed = await scope.ShiftService.CloseShiftAsync(new CloseShiftRequest
        {
            ShiftId = seed.ShiftId,
            CashierId = seed.CashierId,
            TerminalId = seed.TerminalId,
            ActualCashPaisa = 50_000L
        });

        closed.ExpectedCashPaisa.Should().Be(50_000L);
        closed.DiscrepancyPaisa.Should().Be(0);
    }

    [Fact]
    public async Task Case21_UnreconciledCashIn_RemainsVisible()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, _, _) = await SeedTillAsync(scope, 80_000L, 50_000L);

        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(seed.TerminalId, "Till");
        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 5_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        IReadOnlyList<TillCashMovementDto> unrec =
            await scope.CashTransferService.ListTillCashMovementsAsync(
                shiftId: seed.ShiftId,
                unreconciledOnly: true);
        unrec.Should().Contain(m =>
            m.Direction == ShiftCashMovementDirections.In
            && m.AmountPaisa == 5_000L);
    }

    [Fact]
    public async Task Case22_LedgerDrawerMismatch_DoesNotFreezeTill()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) = await SeedTillAsync(scope, 90_000L, 50_000L);

        await scope.CashTransferService.RecordTillCashInAsync(new RecordTillCashInRequest
        {
            ShiftId = seed.ShiftId,
            TillCashAccountId = till.Id,
            AmountPaisa = 10_000L,
            Reason = ShiftCashInReasons.UnregisteredCash
        });

        CashSpendableBalanceDto spendable = await scope.CashAccountService.GetSpendableBalanceAsync(
            till.Id,
            seed.ShiftId);
        spendable.SpendablePaisa.Should().BeGreaterThan(50_000L);
    }

    [Fact]
    public async Task Case23_Expense_RejectBareLegacyCashCode()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        var expenseService = CreateExpenseService(scope);

        Func<Task> act = () => expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = null,
            LoggedByUserId = seed.CashierId,
            Description = "Bare cash",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"B-{Guid.NewGuid():N}"[..12],
            PaymentMethod = "CASH",
            AmountPaisa = 500L
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Case24_PaymentsEngine_NoDuplicateGlPosting()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        (SaleSeedData seed, CashAccountDto till, _) = await SeedTillAsync(scope, 100_000L, 100_000L);
        var expenseService = CreateExpenseService(scope);

        RecordExpenseResult result = await expenseService.RecordShiftExpenseAsync(new RecordExpenseRequest
        {
            ShiftId = seed.ShiftId,
            LoggedByUserId = seed.CashierId,
            Description = "Once",
            ExpenseCategory = ExpenseCategories.Rent,
            ReceiptReference = $"O-{Guid.NewGuid():N}"[..12],
            PaymentMethod = "CASH",
            CashAccountId = till.Id,
            AmountPaisa = 1_000L
        });

        int legCount = await scope.DbContext.GeneralLedgerEntries.CountAsync(
            e => e.TransactionGroupId == result.TransactionGroupId);
        legCount.Should().Be(2);
    }

    [Fact]
    public async Task Case25_HistoricalMonth_DoesNotRewritePriorStock()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext, stockQty: 20m);

        DateTimeOffset beforeSale = DateTimeOffset.UtcNow;

        StockPositionSummaryDto before = await scope.StockPositionService.GetPositionAsync(beforeSale);
        before.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty.Should().Be(20m);

        await scope.SalesService.CompleteSaleAsync(BuildSale(seed, qty: 5m));

        StockPositionSummaryDto historicalBefore =
            await scope.StockPositionService.GetPositionAsync(beforeSale);
        historicalBefore.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty.Should().Be(20m);

        StockPositionSummaryDto now = await scope.StockPositionService.GetPositionAsync();
        now.ByProduct.Single(p => p.ProductId == seed.ProductId).ExpectedQty.Should().Be(15m);
    }

    [Fact]
    public async Task StockCounts_Migration_TablesExist()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        Func<Task> queryCounts = () => scope.DbContext.StockCounts.CountAsync();
        Func<Task> queryLines = () => scope.DbContext.StockCountLines.CountAsync();
        await queryCounts.Should().NotThrowAsync();
        await queryLines.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FinanceTrends_ReturnRealDailySeries()
    {
        await using IntegrationTestScope scope = _fixture.CreateScope();
        await SeedTillAsync(scope, 50_000L, 50_000L);

        DateTimeOffset from = DateTimeOffset.UtcNow.AddDays(-7);
        DateTimeOffset to = DateTimeOffset.UtcNow;
        FinanceTrendSeriesDto trends = await scope.BusinessPositionService.GetFinanceTrendsAsync(
            new BusinessPositionQuery { From = from, To = to });

        trends.TillCashBalance.Should().NotBeEmpty();
        trends.SalesVsExpenses.Should().NotBeEmpty();
        trends.CashInVsOut.Should().HaveCount(trends.TillCashBalance.Count);
        trends.StockValue.Should().NotBeEmpty();
    }

    private static CompleteSaleRequest BuildSale(SaleSeedData seed, decimal qty) => new()
    {
        InvoiceNo = $"INV-{Guid.NewGuid():N}",
        ShiftId = seed.ShiftId,
        TerminalId = seed.TerminalId,
        CashierId = seed.CashierId,
        PaymentMethod = "CASH",
        Lines =
        [
            new SaleLineRequest
            {
                ProductId = seed.ProductId,
                BatchId = seed.BatchId,
                BatchNumber = seed.BatchNumber,
                ProductName = seed.ProductName,
                Quantity = qty,
                UnitPricePaisa = seed.UnitPricePaisa
            }
        ]
    };

    private static ExpenseService CreateExpenseService(IntegrationTestScope scope) =>
        new(
            scope.DbFactory,
            scope.Ambient,
            scope.TransactionService,
            scope.CashAccountService,
            scope.TenantService);

    private static async Task<(SaleSeedData Seed, CashAccountDto Till, CashierShift Shift)> SeedTillAsync(
        IntegrationTestScope scope,
        long glPaisa,
        long drawerPaisa)
    {
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(scope.DbContext);
        await CashAccountSeeder.SeedForTenantAsync(
            scope.DbContext,
            TenantDefaults.MasterTenantId,
            NullLogger.Instance);

        CashAccountDto till = await scope.CashAccountService.EnsureTillAccountsAsync(
            seed.TerminalId,
            $"RegTill-{Guid.NewGuid():N}"[..18]);
        await FundGlAsync(scope, till.AccountCode, glPaisa);

        CashierShift shift = await scope.DbContext.CashierShifts.SingleAsync(s => s.Id == seed.ShiftId);
        shift.ExpectedCashPaisa = drawerPaisa;
        await scope.DbContext.SaveChangesAsync();
        return (seed, till, shift);
    }

    private static Task FundGlAsync(IntegrationTestScope scope, string accountCode, long amountPaisa) =>
        scope.TransactionService.ExecuteInTransactionAsync(ct =>
            scope.TransactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "TEST_FUND",
                    ReferenceNo = $"FUND-{Guid.NewGuid():N}"[..16],
                    ReferenceDetails = "Regression test funding",
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = accountCode,
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

    private static Task<PartyDto> CreateSupplierAsync(IntegrationTestScope scope) =>
        scope.PartyService.CreatePartyAsync(new CreatePartyRequest
        {
            Role = PartyTypes.Supplier,
            Name = $"Sup-{Guid.NewGuid():N}"[..18],
            PhoneNumber = $"03{Guid.NewGuid():N}"[..11],
            Address = "Lahore",
            CreditLimitPaisa = 0
        });
}
