namespace WebPos.Core.Abstractions;

public sealed class BusinessPositionQuery
{
    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }
}

public sealed class BusinessPositionDto
{
    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }

    public long AvailableCashPaisa { get; init; }

    public long BankPaisa { get; init; }

    public long CustomerReceivablePaisa { get; init; }

    public long SupplierPayablePaisa { get; init; }

    public long StockValuePaisa { get; init; }

    public long StockDifferencePaisa { get; init; }

    public long UnrecordedPhysicalCashPaisa { get; init; }

    public long UnregisteredCashLiabilityPaisa { get; init; }

    public long SalesPaisa { get; init; }

    public long PurchasesPaisa { get; init; }

    public long ExpensesPaisa { get; init; }

    public long OwnerCapitalPaisa { get; init; }

    public long LoanOutstandingPaisa { get; init; }

    public long CashInPaisa { get; init; }

    public long CashOutPaisa { get; init; }
}

public sealed class FinanceDailyPoint
{
    public required DateOnly Date { get; init; }

    public long PrimaryPaisa { get; init; }

    public long SecondaryPaisa { get; init; }
}

public sealed class FinanceTrendSeriesDto
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required IReadOnlyList<FinanceDailyPoint> TillCashBalance { get; init; }

    public required IReadOnlyList<FinanceDailyPoint> SalesVsExpenses { get; init; }

    public required IReadOnlyList<FinanceDailyPoint> CashInVsOut { get; init; }

    public required IReadOnlyList<FinanceDailyPoint> StockValue { get; init; }
}

public interface IBusinessPositionService
{
    Task<BusinessPositionDto> GetPositionAsync(
        BusinessPositionQuery query,
        CancellationToken cancellationToken = default);

    Task<CashStockReconciliationDto> GetCashStockReconciliationAsync(
        CancellationToken cancellationToken = default);

    Task<FinanceTrendSeriesDto> GetFinanceTrendsAsync(
        BusinessPositionQuery query,
        CancellationToken cancellationToken = default);
}
