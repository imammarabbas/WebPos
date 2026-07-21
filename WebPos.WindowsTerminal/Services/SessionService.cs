namespace WebPos.WindowsTerminal.Services;

public sealed class SessionService : ISessionService
{
    public Guid ShiftId { get; private set; }

    public Guid CashierId { get; private set; }

    public Guid TerminalId { get; private set; }

    public bool IsActive => ShiftId != Guid.Empty;

    public void SetSession(Guid shiftId, Guid cashierId, Guid terminalId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(shiftId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(cashierId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(terminalId, Guid.Empty);

        ShiftId = shiftId;
        CashierId = cashierId;
        TerminalId = terminalId;
    }
}
