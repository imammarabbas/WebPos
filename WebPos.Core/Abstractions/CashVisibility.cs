namespace WebPos.Core.Abstractions;

/// <summary>
/// Standard cash visibility labels — physical surplus and UnregisteredCash GL are kept separate
/// to avoid double-counting the same money.
/// </summary>
public static class CashVisibility
{
    public static long DifferencePhysicalMinusExpected(long physicalPaisa, long expectedPaisa) =>
        physicalPaisa - expectedPaisa;

    public static long UnrecordedPhysicalSurplus(long recordedTillGlPaisa, long? physicalPaisa) =>
        physicalPaisa is long physical
            ? Math.Max(0L, physical - recordedTillGlPaisa)
            : 0L;
}

public sealed class TillCashVisibilityDto
{
    public required string TillName { get; init; }

    public long RecordedCashPaisa { get; init; }

    public long ExpectedCashPaisa { get; init; }

    public long? PhysicalCashPaisa { get; init; }

    public bool HasPhysicalCount => PhysicalCashPaisa is not null;

    public long DifferencePhysicalMinusExpectedPaisa =>
        PhysicalCashPaisa is long physical
            ? CashVisibility.DifferencePhysicalMinusExpected(physical, ExpectedCashPaisa)
            : 0L;

    public long UnrecordedPhysicalSurplusPaisa =>
        CashVisibility.UnrecordedPhysicalSurplus(RecordedCashPaisa, PhysicalCashPaisa);

    public long UnregisteredCashLiabilityPaisa { get; init; }
}

public sealed class CashStockReconciliationDto
{
    public required IReadOnlyList<TillCashVisibilityDto> Cash { get; init; }

    public required StockPositionSummaryDto Stock { get; init; }
}
