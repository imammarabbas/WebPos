using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class ReportingService : IReportingService
{
    private readonly WebPosDbContext _context;
    private readonly ITenantService _tenantService;

    public ReportingService(WebPosDbContext context, ITenantService tenantService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public async Task<IReadOnlyList<ExpenseCategorySummary>> GetExpenseSummaryByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return await _context.ShiftExpenses
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.LoggedAt >= from
                && e.LoggedAt <= to)
            .GroupBy(e => e.ExpenseCategory)
            .Select(g => new ExpenseCategorySummary
            {
                ExpenseCategory = g.Key,
                TotalAmountPaisa = g.Sum(x => x.AmountPaisa),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmountPaisa)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetMaintenanceSpendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        // Ledger is the SSOT for financial totals. ShiftExpenses is operational detail only;
        // Math.Max previously masked divergence between the two sources.
        string maintenanceAccount = LedgerAccounts.Expense(ExpenseCategories.Maintenance);

        return await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.AccountCode == maintenanceAccount
                && e.CreatedAt >= from
                && e.CreatedAt <= to)
            .SumAsync(e => e.DebitPaisa, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductProfitSummary>> GetGrossProfitByProductAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        // GroupBy/Sum stay on IQueryable so aggregation runs in SQL, not in memory.
        return await _context.SalesItems
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.Invoice.TenantId == tenantId
                && i.Invoice.CreatedAt >= from
                && i.Invoice.CreatedAt <= to)
            .GroupBy(i => new { i.ProductId, ProductName = i.Product.Name })
            .Select(g => new ProductProfitSummary
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                RevenuePaisa = (long)Math.Round(
                    g.Sum(x => x.Quantity * x.UnitPricePaisa),
                    MidpointRounding.AwayFromZero),
                CostPaisa = (long)Math.Round(
                    g.Sum(x => x.Quantity * x.Batch.CostPricePaisa),
                    MidpointRounding.AwayFromZero)
            })
            .OrderByDescending(x => x.RevenuePaisa - x.CostPaisa)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountCashFlowSummary>> GetCashFlowByAccountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.CreatedAt >= from
                && e.CreatedAt <= to)
            .GroupBy(e => e.AccountCode)
            .Select(g => new AccountCashFlowSummary
            {
                AccountCode = g.Key,
                DebitTotalPaisa = g.Sum(x => x.DebitPaisa),
                CreditTotalPaisa = g.Sum(x => x.CreditPaisa)
            })
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> ExportExpenseReportCsvAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseCategorySummary> rows =
            await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        StringBuilder builder = new();
        builder.AppendLine("ExpenseCategory,TotalAmountPaisa,TransactionCount");

        foreach (ExpenseCategorySummary row in rows)
        {
            builder.Append(CsvEscape(row.ExpenseCategory));
            builder.Append(',');
            builder.Append(row.TotalAmountPaisa.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.TransactionCount.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public async Task<string> ExportExpenseReportJsonAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseCategorySummary> rows =
            await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            from,
            to,
            generatedAt = DateTimeOffset.UtcNow,
            categories = rows
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for reporting operations.");
        }
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
