using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;

namespace WebPos.Core.Services;

public sealed class ReportingService : IReportingService
{
    private readonly WebPosDbContext _context;

    public ReportingService(WebPosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ExpenseCategorySummary>> GetExpenseSummaryByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        List<ExpenseCategorySummary> shiftExpenses = await _context.ShiftExpenses
            .AsNoTracking()
            .Where(e => e.LoggedAt >= from && e.LoggedAt <= to)
            .GroupBy(e => e.ExpenseCategory)
            .Select(g => new ExpenseCategorySummary
            {
                ExpenseCategory = g.Key,
                TotalAmountPaisa = g.Sum(x => x.AmountPaisa),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmountPaisa)
            .ToListAsync(cancellationToken);

        return shiftExpenses;
    }

    public async Task<long> GetMaintenanceSpendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        long fromShiftExpenses = await _context.ShiftExpenses
            .AsNoTracking()
            .Where(e => e.ExpenseCategory == ExpenseCategories.Maintenance
                        && e.LoggedAt >= from
                        && e.LoggedAt <= to)
            .SumAsync(e => e.AmountPaisa, cancellationToken);

        string maintenanceAccount = LedgerAccounts.Expense(ExpenseCategories.Maintenance);

        long fromLedger = await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountCode == maintenanceAccount
                        && e.CreatedAt >= from
                        && e.CreatedAt <= to)
            .SumAsync(e => e.DebitPaisa, cancellationToken);

        return Math.Max(fromShiftExpenses, fromLedger);
    }

    public async Task<IReadOnlyList<ProductProfitSummary>> GetGrossProfitByProductAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var salesData = await _context.SalesItems
            .AsNoTracking()
            .Where(i => i.Invoice.CreatedAt >= from && i.Invoice.CreatedAt <= to)
            .Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Quantity,
                i.UnitPricePaisa,
                i.Batch.CostPricePaisa
            })
            .ToListAsync(cancellationToken);

        return salesData
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(g => new ProductProfitSummary
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                RevenuePaisa = g.Sum(x => (long)Math.Round(x.Quantity * x.UnitPricePaisa, MidpointRounding.AwayFromZero)),
                CostPaisa = g.Sum(x => (long)Math.Round(x.Quantity * x.CostPricePaisa, MidpointRounding.AwayFromZero))
            })
            .OrderByDescending(x => x.GrossProfitPaisa)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountCashFlowSummary>> GetCashFlowByAccountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        return await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
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
        IReadOnlyList<ExpenseCategorySummary> rows = await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

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
        IReadOnlyList<ExpenseCategorySummary> rows = await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            from,
            to,
            generatedAt = DateTimeOffset.UtcNow,
            categories = rows
        }, new JsonSerializerOptions { WriteIndented = true });
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
