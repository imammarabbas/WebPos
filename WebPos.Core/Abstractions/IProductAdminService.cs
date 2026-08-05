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

    public string BaseUnit { get; init; } = "PCS";

    public int ConversionMultiplier { get; init; } = 1;

    public bool ShowOnWebshop { get; init; }

    public decimal MinStockQty { get; init; } = 10m;

    /// <summary>Optional opening stock when creating a product.</summary>
    public decimal? OpeningStockQty { get; init; }

    public long? OpeningRetailPricePaisa { get; init; }

    public long? OpeningCostPricePaisa { get; init; }
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

    public int ConversionMultiplier { get; init; }

    public bool ShowOnWebshop { get; init; }

    public decimal MinStockQty { get; init; }

    public decimal AvailableStock { get; init; }
}

public interface IProductAdminService
{
    Task<IReadOnlyList<ProductAdminDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<ProductAdminDto> CreateAsync(UpsertProductRequest request, CancellationToken cancellationToken = default);

    Task<ProductAdminDto> UpdateAsync(Guid productId, UpsertProductRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListBrandsAsync(CancellationToken cancellationToken = default);

    Task RenameBrandAsync(string fromBrand, string toBrand, CancellationToken cancellationToken = default);
}

