namespace WebPos.Core.Models;

public class CashierShift
{
    public Guid Id { get; set; }

    public Guid TerminalId { get; set; }

    public Guid CashierId { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public long OpeningCashPaisa { get; set; }

    public long ExpectedCashPaisa { get; set; }

    public long? ActualBlindCashPaisa { get; set; }

    public long DiscrepancyPaisa { get; set; }

    public string Status { get; set; } = string.Empty;

    public ICollection<ShiftExpense> ShiftExpenses { get; set; } = new List<ShiftExpense>();
}
