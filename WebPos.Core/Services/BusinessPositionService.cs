using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class BusinessPositionService : IBusinessPositionService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly ITenantService _tenantService;
    private readonly ICashAccountService _cashAccounts;
    private readonly IStockPositionService _stockPosition;
    private readonly IReportingService _reporting;

    public BusinessPositionService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        ITenantService tenantService,
        ICashAccountService cashAccounts,
        IStockPositionService stockPosition,
        IReportingService reporting)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _cashAccounts = cashAccounts ?? throw new ArgumentNullException(nameof(cashAccounts));
        _stockPosition = stockPosition ?? throw new ArgumentNullException(nameof(stockPosition));
        _reporting = reporting ?? throw new ArgumentNullException(nameof(reporting));
    }

    public async Task<BusinessPositionDto> GetPositionAsync(
        BusinessPositionQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (query.To < query.From)
        {
            throw new ArgumentException("Query 'to' must be on or after 'from'.");
        }

        CashBalancesSummaryDto cashNow = await _cashAccounts.GetBalancesSummaryAsync(cancellationToken);
        StockPositionSummaryDto stock = await _stockPosition.GetPositionAsync(query.To, cancellationToken);
        ProfitAndLossSummary pnl = await _reporting.GetProfitAndLossAsync(
            query.From,
            query.To,
            cancellationToken);

        Guid tenantId = _tenantService.TenantId;
        long ar = 0;
        long ap = 0;
        long ownerCapital = 0;
        long loanOutstanding = 0;
        long cashIn = 0;
        long cashOut = 0;
        long unrecordedPhysical = 0;
        long posCashSales = 0;
        long supplierPayouts = 0;

        await DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            ar = await SumPartyBalanceAsOfAsync(context, tenantId, PartyTypes.Customer, query.To, ct);
            ap = await SumPartyBalanceAsOfAsync(context, tenantId, PartyTypes.Supplier, query.To, ct);
            ownerCapital = await SumGlAsOfAsync(context, tenantId, LedgerAccounts.OwnerCapital, query.To, ct);
            loanOutstanding = await SumGlAsOfAsync(context, tenantId, LedgerAccounts.LoanPayable, query.To, ct);

            cashIn = await context.ShiftCashMovements.AsNoTracking()
                .Where(m =>
                    m.TenantId == tenantId
                    && m.Direction == ShiftCashMovementDirections.In
                    && m.CreatedAt >= query.From
                    && m.CreatedAt <= query.To)
                .SumAsync(m => (long?)m.AmountPaisa, ct) ?? 0L;

            cashOut = await context.ShiftCashMovements.AsNoTracking()
                .Where(m =>
                    m.TenantId == tenantId
                    && m.Direction == ShiftCashMovementDirections.Out
                    && m.CreatedAt >= query.From
                    && m.CreatedAt <= query.To)
                .SumAsync(m => (long?)m.AmountPaisa, ct) ?? 0L;

            var periodGl = await context.GeneralLedgerEntries.AsNoTracking()
                .Where(e =>
                    e.TenantId == tenantId
                    && e.CreatedAt >= query.From
                    && e.CreatedAt <= query.To)
                .Select(e => new
                {
                    e.TransactionType,
                    e.AccountCode,
                    e.DebitPaisa,
                    e.CreditPaisa
                })
                .ToListAsync(ct);

            posCashSales = periodGl
                .Where(e =>
                    string.Equals(e.TransactionType, "SALE", StringComparison.OrdinalIgnoreCase)
                    && LedgerAccounts.IsCashAccount(e.AccountCode))
                .Sum(e => e.DebitPaisa - e.CreditPaisa);
            supplierPayouts = periodGl
                .Where(e =>
                    string.Equals(e.TransactionType, "SUPPLIER_PAYMENT", StringComparison.OrdinalIgnoreCase)
                    && e.CreditPaisa > 0)
                .Sum(e => e.CreditPaisa);

            foreach (CashAccountCardDto till in cashNow.Accounts.Where(a =>
                         a.Account.Type == CashAccountType.Till && a.Account.IsActive))
            {
                unrecordedPhysical += CashVisibility.UnrecordedPhysicalSurplus(
                    till.DerivedGlBalancePaisa,
                    till.PhysicalBlindCashPaisa);
            }
        }, cancellationToken);

        long availableCash = cashNow.Accounts
            .Where(a => a.Account.Type == CashAccountType.Till && a.Account.IsActive)
            .Sum(a => a.SpendablePaisa);
        long bank = cashNow.InBankPaisa;
        long unregisteredGl = await _cashAccounts.GetAccountBalancePaisaByCodeAsync(
            LedgerAccounts.UnregisteredCash,
            cancellationToken: cancellationToken);

        return new BusinessPositionDto
        {
            From = query.From,
            To = query.To,
            AvailableCashPaisa = availableCash,
            BankPaisa = bank,
            CustomerReceivablePaisa = ar,
            SupplierPayablePaisa = ap,
            StockValuePaisa = stock.ExpectedValuePaisa,
            StockDifferencePaisa = stock.MissingUnexplainedValuePaisa,
            UnrecordedPhysicalCashPaisa = unrecordedPhysical,
            UnregisteredCashLiabilityPaisa = unregisteredGl,
            SalesPaisa = pnl.RevenuePaisa,
            PurchasesPaisa = pnl.CostPaisa,
            ExpensesPaisa = pnl.OperatingExpensesPaisa,
            OwnerCapitalPaisa = ownerCapital,
            LoanOutstandingPaisa = loanOutstanding,
            CashInPaisa = cashIn,
            CashOutPaisa = cashOut,
            PosCashSalesPaisa = posCashSales,
            SupplierPayoutsPaisa = supplierPayouts
        };
    }

    public async Task<CashStockReconciliationDto> GetCashStockReconciliationAsync(
        CancellationToken cancellationToken = default)
    {
        CashBalancesSummaryDto cash = await _cashAccounts.GetBalancesSummaryAsync(cancellationToken);
        StockPositionSummaryDto stock = await _stockPosition.GetPositionAsync(cancellationToken: cancellationToken);
        long unregisteredGl = await _cashAccounts.GetAccountBalancePaisaByCodeAsync(
            LedgerAccounts.UnregisteredCash,
            cancellationToken: cancellationToken);

        var tillRows = cash.Accounts
            .Where(a => a.Account.Type == CashAccountType.Till && a.Account.IsActive)
            .Select(a => new TillCashVisibilityDto
            {
                TillName = a.Account.Name,
                RecordedCashPaisa = a.DerivedGlBalancePaisa,
                ExpectedCashPaisa = a.DrawerExpectedCashPaisa ?? 0,
                PhysicalCashPaisa = a.PhysicalBlindCashPaisa,
                UnregisteredCashLiabilityPaisa = unregisteredGl
            })
            .ToList();

        return new CashStockReconciliationDto
        {
            Cash = tillRows,
            Stock = stock
        };
    }

    public async Task<FinanceTrendSeriesDto> GetFinanceTrendsAsync(
        BusinessPositionQuery query,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (query.To < query.From)
        {
            throw new ArgumentException("Query 'to' must be on or after 'from'.");
        }

        Guid tenantId = _tenantService.TenantId;
        DateOnly fromDate = DateOnly.FromDateTime(query.From.UtcDateTime);
        DateOnly toDate = DateOnly.FromDateTime(query.To.UtcDateTime);

        List<DateOnly> days = [];
        for (DateOnly d = fromDate; d <= toDate; d = d.AddDays(1))
        {
            days.Add(d);
        }

        if (days.Count == 0)
        {
            return EmptyTrends(query);
        }

        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<string> tillCodes = await context.CashAccounts
            .AsNoTracking()
            .Where(a =>
                a.TenantId == tenantId
                && a.Type == CashAccountType.Till
                && a.IsActive)
            .Select(a => a.AccountCode.ToUpper())
            .ToListAsync(cancellationToken);

        List<GeneralLedgerEntry> tillEntries = tillCodes.Count == 0
            ? []
            : await context.GeneralLedgerEntries
                .AsNoTracking()
                .Where(e =>
                    e.TenantId == tenantId
                    && tillCodes.Contains(e.AccountCode.ToUpper())
                    && e.CreatedAt <= query.To)
                .Select(e => new GeneralLedgerEntry
                {
                    AccountCode = e.AccountCode,
                    DebitPaisa = e.DebitPaisa,
                    CreditPaisa = e.CreditPaisa,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync(cancellationToken);

        // Operating expenses from GL EXPENSE:* debits (excludes discount category handled in P&L).
        var expenseRows = await context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.AccountCode.StartsWith("EXPENSE:")
                && e.AccountCode != LedgerAccounts.Expense(ExpenseCategories.Discount)
                && e.CreatedAt >= query.From
                && e.CreatedAt <= query.To)
            .Select(e => new { e.DebitPaisa, e.CreatedAt })
            .ToListAsync(cancellationToken);

        Dictionary<DateOnly, long> salesByDay = await context.SalesInvoices
            .AsNoTracking()
            .Where(inv =>
                inv.TenantId == tenantId
                && inv.CreatedAt >= query.From
                && inv.CreatedAt <= query.To)
            .GroupBy(inv => DateOnly.FromDateTime(inv.CreatedAt.UtcDateTime))
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.TotalAmountPaisa) })
            .ToDictionaryAsync(x => x.Date, x => x.Total, cancellationToken);

        Dictionary<DateOnly, long> expensesByDay = expenseRows
            .GroupBy(e => DateOnly.FromDateTime(e.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.DebitPaisa));

        var cashMovements = await context.ShiftCashMovements
            .AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId
                && m.CreatedAt >= query.From
                && m.CreatedAt <= query.To)
            .Select(m => new { m.Direction, m.AmountPaisa, m.CreatedAt })
            .ToListAsync(cancellationToken);

        Dictionary<DateOnly, long> cashInByDay = cashMovements
            .Where(m => m.Direction == ShiftCashMovementDirections.In)
            .GroupBy(m => DateOnly.FromDateTime(m.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaisa));

        Dictionary<DateOnly, long> cashOutByDay = cashMovements
            .Where(m => m.Direction == ShiftCashMovementDirections.Out)
            .GroupBy(m => DateOnly.FromDateTime(m.CreatedAt.UtcDateTime))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AmountPaisa));

        List<FinanceDailyPoint> tillSeries = [];
        List<FinanceDailyPoint> salesSeries = [];
        List<FinanceDailyPoint> cashIoSeries = [];
        List<FinanceDailyPoint> stockSeries = [];

        foreach (DateOnly day in days)
        {
            DateTimeOffset endOfDay = new(
                day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));

            long tillBalance = tillCodes.Count == 0
                ? 0
                : tillEntries
                    .Where(e => e.CreatedAt <= endOfDay)
                    .Sum(e => e.DebitPaisa - e.CreditPaisa);

            tillSeries.Add(new FinanceDailyPoint
            {
                Date = day,
                PrimaryPaisa = tillBalance,
                SecondaryPaisa = 0
            });

            salesSeries.Add(new FinanceDailyPoint
            {
                Date = day,
                PrimaryPaisa = salesByDay.GetValueOrDefault(day),
                SecondaryPaisa = expensesByDay.GetValueOrDefault(day)
            });

            cashIoSeries.Add(new FinanceDailyPoint
            {
                Date = day,
                PrimaryPaisa = cashInByDay.GetValueOrDefault(day),
                SecondaryPaisa = cashOutByDay.GetValueOrDefault(day)
            });

            StockPositionSummaryDto stock = await _stockPosition.GetPositionAsync(endOfDay, cancellationToken);
            stockSeries.Add(new FinanceDailyPoint
            {
                Date = day,
                PrimaryPaisa = stock.ExpectedValuePaisa,
                SecondaryPaisa = stock.MissingUnexplainedValuePaisa
            });
        }

        return new FinanceTrendSeriesDto
        {
            From = query.From,
            To = query.To,
            TillCashBalance = tillSeries,
            SalesVsExpenses = salesSeries,
            CashInVsOut = cashIoSeries,
            StockValue = stockSeries
        };
    }

    private static FinanceTrendSeriesDto EmptyTrends(BusinessPositionQuery query) =>
        new()
        {
            From = query.From,
            To = query.To,
            TillCashBalance = [],
            SalesVsExpenses = [],
            CashInVsOut = [],
            StockValue = []
        };

    private static async Task<long> SumGlAsOfAsync(
        WebPosDbContext context,
        Guid tenantId,
        string accountCode,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.AccountCode.ToUpper() == code
                && e.CreatedAt <= asOf);

        long? debits = await query.SumAsync(e => (long?)e.DebitPaisa, cancellationToken);
        long? credits = await query.SumAsync(e => (long?)e.CreditPaisa, cancellationToken);
        return (debits ?? 0L) - (credits ?? 0L);
    }

    private static async Task<long> SumPartyBalanceAsOfAsync(
        WebPosDbContext context,
        Guid tenantId,
        string partyType,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var parties = await context.Parties.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PartyType == partyType)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (parties.Count == 0)
        {
            return 0;
        }

        DateTime asOfUtc = asOf.UtcDateTime;
        var ledger = await context.PartyLedgers.AsNoTracking()
            .Where(l =>
                l.TenantId == tenantId
                && parties.Contains(l.PartyId)
                && l.CreatedAt <= asOfUtc)
            .GroupBy(l => l.PartyId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First().NewBalancePaisa)
            .ToListAsync(cancellationToken);

        return ledger.Where(b => b > 0).Sum();
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is not resolved.");
        }
    }
}
