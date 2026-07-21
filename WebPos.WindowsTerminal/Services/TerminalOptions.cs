namespace WebPos.WindowsTerminal.Services;

public sealed class TerminalOptions(Guid tenantId, Guid terminalId)
{
    public Guid TenantId { get; } = tenantId != Guid.Empty
        ? tenantId
        : throw new ArgumentException(
            "TenantId must be supplied by a validated enrollment certificate.",
            nameof(tenantId));

    public Guid TerminalId { get; } = terminalId != Guid.Empty
        ? terminalId
        : throw new ArgumentException(
            "TerminalId must be supplied by a validated enrollment certificate.",
            nameof(terminalId));
}
