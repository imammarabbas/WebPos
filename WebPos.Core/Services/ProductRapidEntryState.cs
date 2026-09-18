using WebPos.Core;

namespace WebPos.Core.Services;

/// <summary>
/// Circuit-scoped sticky fields and recently created SKUs for high-volume product create.
/// </summary>
public sealed class ProductRapidEntryState
{
    private readonly List<RecentProductEntry> _recent = [];

    public ProductKind Kind { get; set; } = ProductKind.Independent;

    public string CategoryId { get; set; } = string.Empty;

    public string ParentProductId { get; set; } = string.Empty;

    public IReadOnlyList<RecentProductEntry> Recent => _recent;

    public void Capture(ProductKind kind, string categoryId, string parentProductId)
    {
        Kind = kind;
        CategoryId = categoryId ?? string.Empty;
        if (kind == ProductKind.Child)
        {
            ParentProductId = parentProductId ?? string.Empty;
        }
    }

    public void RememberCreated(RecentProductEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _recent.RemoveAll(existing => existing.Id == entry.Id);
        _recent.Insert(0, entry);
        const int keep = 10;
        if (_recent.Count > keep)
        {
            _recent.RemoveRange(keep, _recent.Count - keep);
        }
    }
}

public sealed class RecentProductEntry
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Barcode { get; init; }

    public required ProductKind Kind { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
