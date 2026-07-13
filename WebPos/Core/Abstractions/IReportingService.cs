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
}

public sealed class AccountCashFlowSummary
{
    public required string AccountCode { get; init; }

    public long DebitTotalPaisa { get; init; }

    public long CreditTotalPaisa { get; init; }

    public long NetFlowPaisa => DebitTotalPaisa - CreditTotalPaisa;
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

    Task<IReadOnlyList<AccountCashFlowSummary>> GetCashFlowByAccountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
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
