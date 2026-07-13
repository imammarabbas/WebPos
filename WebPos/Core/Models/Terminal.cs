namespace WebPos.Core.Models;

public class Terminal
{
    public Guid Id { get; set; }

    public string TerminalName { get; set; } = string.Empty;

    public string MacAddress { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset LastSyncTime { get; set; }
}
