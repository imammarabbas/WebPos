namespace WebPos.WindowsTerminal.Services;

public interface ISessionService
{
    Guid ShiftId { get; }

    Guid CashierId { get; }

    Guid TerminalId { get; }

    bool IsActive { get; }

    void SetSession(Guid shiftId, Guid cashierId, Guid terminalId);
}
