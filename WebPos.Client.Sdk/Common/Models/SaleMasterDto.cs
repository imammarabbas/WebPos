namespace Common.Models;

/// <summary>
/// Bulk parent shown on the POS quick strip. Not sellable itself —
/// cashiers pick a <see cref="Children"/> packing variant.
/// </summary>
public sealed class SaleMasterDto
{
    public Guid ProductId { get; init; }

    public required string Name { get; init; }

    public required string ShortCode { get; init; }

    public required string Barcode { get; init; }

    public required string Sku { get; init; }

    public string BaseUnit { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    private readonly IReadOnlyList<SaleVariantChildDto>? _children;

    public IReadOnlyList<SaleVariantChildDto> Children
    {
        get => _children ?? [];
        init => _children = value;
    }
}

/// <summary>Sellable child packing under a bulk master.</summary>
public sealed class SaleVariantChildDto
{
    public Guid ProductId { get; init; }

    public required string Name { get; init; }

    public required string PackingSize { get; init; }

    public required string Barcode { get; init; }

    public required string ShortCode { get; init; }

    public required string Sku { get; init; }

    public long UnitPricePaisa { get; init; }

    public decimal AvailableStock { get; init; }

    public bool IsLoose { get; init; }

    public string BaseUnit { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;
}
