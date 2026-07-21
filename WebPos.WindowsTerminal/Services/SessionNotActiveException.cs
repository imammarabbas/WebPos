namespace WebPos.WindowsTerminal.Services;

public sealed class SessionNotActiveException()
    : InvalidOperationException("Start Shift before processing a sale.");
