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

    public long ActualCashPaisa { get; init; }
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
}

public sealed class CashVarianceReport
{
    public Guid ShiftId { get; init; }

    public Guid TerminalId { get; init; }

    public Guid CashierId { get; init; }

    public required string Status { get; init; }

    public long OpeningCashPaisa { get; init; }

    public long ExpectedCashPaisa { get; init; }

    public long ActualCashPaisa { get; init; }

    public long DiscrepancyPaisa { get; init; }

    public bool IsBalanced { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }
}
