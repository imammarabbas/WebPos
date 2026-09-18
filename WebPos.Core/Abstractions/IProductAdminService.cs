using WebPos.Core.Abstractions;

namespace WebPos.Core.Abstractions;

public sealed class UpsertProductRequest
{
    public Guid? CategoryId { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public string Barcode { get; init; } = string.Empty;

    public string ShortCode { get; init; } = string.Empty;

    public bool IsLoose { get; init; }

    public string Brand { get; init; } = string.Empty;

    /// <summary>Sale / stock unit (e.g. g).</summary>
    public string BaseUnit { get; init; } = "PCS";

    /// <summary>Purchase unit (e.g. kg). Empty = same as sale unit.</summary>
    public string PurchaseUnit { get; init; } = string.Empty;

    /// <summary>Sale units per 1 purchase unit (e.g. 1000 for kg→g).</summary>
    public int ConversionMultiplier { get; init; } = 1;

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public decimal MinStockQty { get; init; } = 10m;

    /// <summary>Optional opening stock when creating a master product.</summary>
    public decimal? OpeningStockQty { get; init; }

    public long? OpeningRetailPricePaisa { get; init; }

    public long? OpeningCostPricePaisa { get; init; }

    /// <summary>On update: set master pool / catalog cost.</summary>
    public long? CostPricePaisa { get; init; }

    /// <summary>On update: set catalog retail.</summary>
    public long? RetailPricePaisa { get; init; }

    /// <summary>Bulk warehouse SKU that may be used as a child-variant parent.</summary>
    public bool IsBulk { get; init; }

    /// <summary>Default child-pack margin percent on a bulk parent (e.g. 20).</summary>
    public decimal DefaultMarginPercent { get; init; } = 20m;

    /// <summary>When set, product is a virtual alias of this master.</summary>
    public Guid? ParentProductId { get; init; }

    /// <summary>Parent base units per 1 alias unit (required when parent set).</summary>
    public decimal? DeductionMultiplier { get; init; }
}

public sealed class ProductAdminDto
{
    public required Guid Id { get; init; }

    public Guid? CategoryId { get; init; }

    public string? CategoryName { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public required string Barcode { get; init; }

    public required string ShortCode { get; init; }

    public bool IsLoose { get; init; }

    public required string Brand { get; init; }

    public required string BaseUnit { get; init; }

    public required string PurchaseUnit { get; init; }

    public int ConversionMultiplier { get; init; }

    public bool ShowOnWebshop { get; init; }

    public bool ShowOnPosQuick { get; init; }

    public int PosQuickSort { get; init; }

    public decimal MinStockQty { get; init; }

    public decimal AvailableStock { get; init; }

    public long LatestCostPricePaisa { get; init; }

    public long LatestRetailPricePaisa { get; init; }

    public bool IsBulk { get; init; }

    public decimal DefaultMarginPercent { get; init; }

    public Guid? ParentProductId { get; init; }

    public string? ParentProductName { get; init; }

    public decimal? DeductionMultiplier { get; init; }
}

public sealed class ProductSearchQuery
{
    public string? Search { get; init; }

    public bool LowStockOnly { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}

public interface IProductAdminService
{
    Task<IReadOnlyList<ProductAdminDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<ProductAdminDto>> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductAdminDto>> ListBulkParentsAsync(
        string? search,
        int take = 50,
        Guid? includeId = null,
        CancellationToken cancellationToken = default);

    Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductAdminDto> CreateAsync(UpsertProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductAdminDto> UpdateAsync(Guid productId, UpsertProductRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListBrandsAsync(CancellationToken cancellationToken = default);

    Task RenameBrandAsync(string fromBrand, string toBrand, CancellationToken cancellationToken = default);
}
