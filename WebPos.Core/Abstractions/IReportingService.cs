namespace WebPos.Core.Abstractions;

public sealed class ExpenseCategorySummary
{
    public required string ExpenseCategory { get; init; }

    public long TotalAmountPaisa { get; init; }

    public int TransactionCount { get; init; }
}

public sealed class ProductProfitSummary
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public long RevenuePaisa { get; init; }

    public long CostPaisa { get; init; }

    public long GrossProfitPaisa => RevenuePaisa - CostPaisa;

    public decimal MarginPercent =>
        RevenuePaisa == 0
            ? 0m
            : Math.Round(GrossProfitPaisa * 100m / RevenuePaisa, 2, MidpointRounding.AwayFromZero);
}

public sealed class CategoryProfitSummary
{
    public required Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }

    public long RevenuePaisa { get; init; }

    public long CostPaisa { get; init; }

    public long GrossProfitPaisa => RevenuePaisa - CostPaisa;

    public decimal MarginPercent =>
        RevenuePaisa == 0
            ? 0m
            : Math.Round(GrossProfitPaisa * 100m / RevenuePaisa, 2, MidpointRounding.AwayFromZero);
}

public sealed class AccountCashFlowSummary
{
    public required string AccountCode { get; init; }

    public long DebitTotalPaisa { get; init; }

    public long CreditTotalPaisa { get; init; }

    public long NetFlowPaisa => DebitTotalPaisa - CreditTotalPaisa;
}

public sealed class ProfitAndLossSummary
{
    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public long RevenuePaisa { get; init; }

    public long CostPaisa { get; init; }

    public long GrossProfitPaisa => RevenuePaisa - CostPaisa;

    public decimal GrossMarginPercent =>
        RevenuePaisa == 0
            ? 0m
            : Math.Round(GrossProfitPaisa * 100m / RevenuePaisa, 2, MidpointRounding.AwayFromZero);

    public long OperatingExpensesPaisa { get; init; }

    public long NetProfitPaisa => GrossProfitPaisa - OperatingExpensesPaisa;

    public required IReadOnlyList<ExpenseCategorySummary> ExpensesByCategory { get; init; }

    public required IReadOnlyList<ProductProfitSummary> ProductProfits { get; init; }

    public required IReadOnlyList<CategoryProfitSummary> CategoryProfits { get; init; }
}

public interface IReportingService
{
    Task<IReadOnlyList<ExpenseCategorySummary>> GetExpenseSummaryByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<long> GetMaintenanceSpendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductProfitSummary>> GetGrossProfitByProductAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryProfitSummary>> GetGrossProfitByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountCashFlowSummary>> GetCashFlowByAccountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<ProfitAndLossSummary> GetProfitAndLossAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<BiDashboardResult> GetBiDashboardAsync(
        BiDashboardQuery query,
        CancellationToken cancellationToken = default);

    Task<string> ExportExpenseReportCsvAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<string> ExportExpenseReportJsonAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
