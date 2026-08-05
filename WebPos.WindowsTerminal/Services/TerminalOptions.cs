namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Holds tenant/terminal identity from enrollment. Empty until enrolled.
/// </summary>
public sealed class TerminalOptions
{
    public Guid TenantId { get; private set; }

    public Guid TerminalId { get; private set; }

    public bool IsEnrolled =>
        TenantId != Guid.Empty && TerminalId != Guid.Empty;

    public void ApplyEnrollment(Guid tenantId, Guid terminalId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(tenantId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(terminalId, Guid.Empty);
        TenantId = tenantId;
        TerminalId = terminalId;
    }

    public void Clear()
    {
        TenantId = Guid.Empty;
        TerminalId = Guid.Empty;
    }
}
