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

    public long UnitPricePaisa { get; init; }

    public decimal AvailableStock { get; init; }

    public DateOnly? ExpiryDate { get; init; }
}
