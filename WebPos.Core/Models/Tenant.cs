namespace WebPos.Core.Models;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Display name on Windows Terminal / receipts (defaults to <see cref="Name"/>).</summary>
    public string PosDisplayName { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
