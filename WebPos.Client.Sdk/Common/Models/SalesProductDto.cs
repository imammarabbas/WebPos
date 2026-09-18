namespace Common.Models;

/// <summary>
/// A sellable product batch with current pricing and stock information.
/// </summary>
public sealed class SalesProductDto
{
    public Guid ProductId { get; init; }

    public Guid BatchId { get; init; }

    public required string BatchNumber { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public required string Barcode { get; init; }

    public required string ShortCode { get; init; }

    public required string CategoryName { get; init; }

    public bool IsLoose { get; init; }

    /// <summary>Sale / stock unit label (e.g. g).</summary>
    public string BaseUnit { get; init; } = string.Empty;

    /// <summary>
    /// Display packing (e.g. 500 ML, 1kg). Empty when not applicable.
    /// </summary>
    public string PackingSize { get; init; } = string.Empty;

    public long UnitPricePaisa { get; init; }

    public decimal AvailableStock { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    /// <summary>True when this SKU is a bulk warehouse / parent product.</summary>
    public bool IsBulk { get; init; }

    /// <summary>
    /// Child pack names, barcodes, SKUs, and short codes used to find this master
    /// when receiving stock. Empty for ordinary sale catalog rows.
    /// </summary>
    public IReadOnlyList<string> AliasSearchTerms { get; init; } = [];
}
