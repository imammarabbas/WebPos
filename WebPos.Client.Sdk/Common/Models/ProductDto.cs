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

    public string PurchaseUnit { get; init; } = string.Empty;

    public int ConversionMultiplier { get; init; }

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public decimal MinStockQty { get; init; }

    public decimal StockQty { get; init; }

    public long CostPricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public decimal DefaultMarginPercent { get; init; } = 20m;

    public bool IsBulk { get; init; }

    public Guid? ParentProductId { get; init; }

    public decimal? DeductionMultiplier { get; init; }
}
