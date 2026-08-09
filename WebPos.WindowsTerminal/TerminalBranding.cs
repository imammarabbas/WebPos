namespace WebPos.WindowsTerminal;

public static class TerminalBranding
{
    public const string StoreName = "Cone Mart";

    /// <summary>Mutable POS header name; defaults to <see cref="StoreName"/>.</summary>
    public static string PosDisplayName { get; set; } = StoreName;
}
