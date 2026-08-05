namespace WebPos.WindowsTerminal.Services;

public sealed class SessionService : ISessionService
{
    public Guid ShiftId { get; private set; }

    public Guid CashierId { get; private set; }

    public Guid TerminalId { get; private set; }

    public string CashierName { get; private set; } = string.Empty;

    public DateTimeOffset? OpenedAt { get; private set; }

    public long ExpectedCashPaisa { get; private set; }

    public bool IsActive => ShiftId != Guid.Empty;

    public void SetSession(
        Guid shiftId,
        Guid cashierId,
        Guid terminalId,
        string cashierName,
        DateTimeOffset openedAt,
        long expectedCashPaisa = 0)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(shiftId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(cashierId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(terminalId, Guid.Empty);

        ShiftId = shiftId;
        CashierId = cashierId;
        TerminalId = terminalId;
        CashierName = string.IsNullOrWhiteSpace(cashierName) ? "Cashier" : cashierName.Trim();
        OpenedAt = openedAt;
        ExpectedCashPaisa = expectedCashPaisa;
    }

    public void UpdateExpectedCash(long expectedCashPaisa)
    {
        ExpectedCashPaisa = Math.Max(0, expectedCashPaisa);
    }

    public void ClearSession()
    {
        ShiftId = Guid.Empty;
        CashierId = Guid.Empty;
        TerminalId = Guid.Empty;
        CashierName = string.Empty;
        OpenedAt = null;
        ExpectedCashPaisa = 0;
    }
}
