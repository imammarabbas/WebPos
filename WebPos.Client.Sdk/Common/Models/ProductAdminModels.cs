namespace Common.Models;

/// <summary>Create/update product body for api/products (mirrors server UpsertProductRequest).</summary>
public sealed class UpsertProductRequest
{
    public Guid? CategoryId { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public string Barcode { get; init; } = string.Empty;

    public string ShortCode { get; init; } = string.Empty;

    public bool IsLoose { get; init; }

    public string Brand { get; init; } = string.Empty;

    public string BaseUnit { get; init; } = "PCS";

    public string PurchaseUnit { get; init; } = string.Empty;

    public int ConversionMultiplier { get; init; } = 1;

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public decimal MinStockQty { get; init; } = 10m;

    public decimal? OpeningStockQty { get; init; }

    public long? OpeningRetailPricePaisa { get; init; }

    public long? OpeningCostPricePaisa { get; init; }

    public long? CostPricePaisa { get; init; }

    public long? RetailPricePaisa { get; init; }

    public bool IsBulk { get; init; }

    public decimal DefaultMarginPercent { get; init; } = 20m;

    public Guid? ParentProductId { get; init; }

    public decimal? DeductionMultiplier { get; init; }
}

public sealed class CategoryDto
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public Guid? ParentCategoryId { get; init; }

    public int TargetMarginPercentage { get; init; }

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public int ProductCount { get; init; }
}

/// <summary>Server-paged product catalog search result.</summary>
public sealed class PagedProductResult
{
    public required IReadOnlyList<ProductDto> Items { get; init; }

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}

/// <summary>POS Quick Items strip entry (category, bulk master, or sellable SKU).</summary>
public sealed class SaleQuickLinkDto
{
    /// <summary>Category | Master | Product</summary>
    public required string LinkType { get; init; }

    public Guid TargetId { get; init; }

    public required string Name { get; init; }

    public int SortOrder { get; init; }

    /// <summary>Populated when <see cref="LinkType"/> is Product (independent or child pack).</summary>
    public SalesProductDto? Product { get; init; }
}
