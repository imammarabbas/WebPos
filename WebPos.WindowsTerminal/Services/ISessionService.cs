namespace WebPos.WindowsTerminal.Services;

public interface ISessionService
{
    Guid ShiftId { get; }

    Guid CashierId { get; }

    Guid TerminalId { get; }

    string CashierName { get; }

    DateTimeOffset? OpenedAt { get; }

    long ExpectedCashPaisa { get; }

    bool IsActive { get; }

    void SetSession(
        Guid shiftId,
        Guid cashierId,
        Guid terminalId,
        string cashierName,
        DateTimeOffset openedAt,
        long expectedCashPaisa = 0);

    void UpdateExpectedCash(long expectedCashPaisa);

    void ClearSession();
}
