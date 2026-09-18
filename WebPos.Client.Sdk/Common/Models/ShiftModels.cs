namespace Common.Models;

public sealed class StartShiftRequest
{
    public Guid CashierId { get; init; }

    public Guid TerminalId { get; init; }

    public long OpeningCashPaisa { get; init; }
}

public sealed class CloseShiftRequest
{
    public Guid ShiftId { get; init; }

    public Guid CashierId { get; init; }

    public Guid TerminalId { get; init; }

    /// <summary>Blind physical cash count (paisa). Stored as ActualBlindCashPaisa; never overwrites ExpectedCash.</summary>
    public long ActualCashPaisa { get; init; }

    /// <summary>
    /// Obsolete. Ledger vs ExpectedCash gaps are handled mid-shift via Cash In / Shortage.
    /// Ignored at close — close uses PhysicalCount vs ExpectedCash only.
    /// </summary>
    public long? ResolveLedgerShortagePaisa { get; init; }
}

public sealed class ShiftDto
{
    public Guid ShiftId { get; init; }

    public Guid CashierId { get; init; }

    public Guid TerminalId { get; init; }

    public DateTimeOffset OpenedAt { get; init; }

    public required string Status { get; init; }
}

public sealed class OpenShiftDto
{
    public Guid ShiftId { get; init; }

    public Guid CashierId { get; init; }

    public required string CashierName { get; init; }

    public Guid TerminalId { get; init; }

    public string? TerminalName { get; init; }

    public DateTimeOffset OpenedAt { get; init; }

    public long OpeningCashPaisa { get; init; }

    public long ExpectedCashPaisa { get; init; }

    /// <summary>Till GL (Registered).</summary>
    public long RegisteredLedgerPaisa { get; init; }

    /// <summary>Available = min(Registered, ExpectedCash).</summary>
    public long AvailablePaisa { get; init; }

    public long UnreconciledCashInPaisa { get; init; }

    public long UnreconciledCashOutPaisa { get; init; }
}

public sealed class SuggestedOpeningCashDto
{
    public Guid TerminalId { get; init; }

    /// <summary>Current Till GL — suggested opening when cash remains in the till.</summary>
    public long TillGlPaisa { get; init; }

    public long? LastExpectedCashPaisa { get; init; }

    public long? LastPhysicalCountPaisa { get; init; }

    /// <summary>Recommended default for Opening/Expected (Till GL).</summary>
    public long SuggestedOpeningPaisa { get; init; }
}

public sealed class CashVarianceReport
{
    public Guid ShiftId { get; init; }

    public Guid TerminalId { get; init; }

    public Guid CashierId { get; init; }

    /// <summary>Shift status (OPEN/CLOSED).</summary>
    public required string Status { get; init; }

    public long OpeningCashPaisa { get; init; }

    /// <summary>System ExpectedCash — never overwritten by blind count.</summary>
    public long ExpectedCashPaisa { get; init; }

    /// <summary>Blind physical count (same as PhysicalCountPaisa).</summary>
    public long ActualCashPaisa { get; init; }

    /// <summary>Alias of ActualCashPaisa for Clear UI naming.</summary>
    public long PhysicalCountPaisa { get; init; }

    /// <summary>PhysicalCount − ExpectedCash.</summary>
    public long DiscrepancyPaisa { get; init; }

    public bool IsBalanced { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public long RegisteredLedgerPaisa { get; init; }

    public long AvailablePaisa { get; init; }

    public long UnreconciledCashInPaisa { get; init; }

    public long UnreconciledCashOutPaisa { get; init; }

    /// <summary>MATCHED | SHORTAGE | OVERAGE | UNRECONCILED</summary>
    public required string ReconciliationStatus { get; init; }
}
