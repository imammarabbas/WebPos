namespace Common.Models;

public sealed class ProductDto
{
    public Guid Id { get; init; }

    public Guid? CategoryId { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public required string Barcode { get; init; }

    public required string ShortCode { get; init; }

    public bool IsLoose { get; init; }

    public required string Brand { get; init; }

    public required string BaseUnit { get; init; }

    public int ConversionMultiplier { get; init; }

    public bool ShowOnWebshop { get; init; }

    public decimal MinStockQty { get; init; }
}
