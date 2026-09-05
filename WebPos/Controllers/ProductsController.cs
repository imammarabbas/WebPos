using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
[TenantAuthorize]
public sealed class ProductsController(
    WebPosDbContext context,
    IProductAdminService productAdminService) : ControllerBase
{
    private readonly WebPosDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
    private readonly IProductAdminService _productAdminService =
        productAdminService ?? throw new ArgumentNullException(nameof(productAdminService));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductAdminDto> products = await _productAdminService.ListAsync(cancellationToken);
        return Ok(products.Select(ToCommonDto).ToList());
    }

    [HttpGet("brands")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetBrands(CancellationToken cancellationToken)
    {
        return Ok(await _productAdminService.ListBrandsAsync(cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] UpsertProductRequest request,
        CancellationToken cancellationToken)
    {
        ProductAdminDto product = await _productAdminService.CreateAsync(request, cancellationToken);
        return Ok(ToCommonDto(product));
    }

    [HttpPut("{productId:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(
        Guid productId,
        [FromBody] UpsertProductRequest request,
        CancellationToken cancellationToken)
    {
        ProductAdminDto product = await _productAdminService.UpdateAsync(productId, request, cancellationToken);
        return Ok(ToCommonDto(product));
    }

    [HttpPost("brands/rename")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RenameBrand(
        [FromBody] RenameBrandRequest request,
        CancellationToken cancellationToken)
    {
        await _productAdminService.RenameBrandAsync(request.FromBrand, request.ToBrand, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// On-demand barcode lookup for terminals: prefer a sellable batch, else latest batch.
    /// </summary>
    [HttpGet("by-barcode/{barcode}")]
    [ProducesResponseType(typeof(SalesProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalesProductDto>> GetProductByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        string normalized = Uri.UnescapeDataString(barcode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest("Barcode is required.");
        }

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Barcode == normalized)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.BaseUnit,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                Sellable = p.Batches
                    .Where(b =>
                        b.CurrentQty > 0
                        && b.RetailPricePaisa > 0
                        && (b.ExpiryDate == null || b.ExpiryDate >= today))
                    .OrderBy(b => b.ExpiryDate)
                    .ThenBy(b => b.CreatedAt)
                    .Select(b => new
                    {
                        b.Id,
                        b.BatchNumber,
                        b.RetailPricePaisa,
                        b.CurrentQty,
                        b.ExpiryDate
                    })
                    .FirstOrDefault(),
                Latest = p.Batches
                    .OrderByDescending(b => b.CreatedAt)
                    .Select(b => new
                    {
                        b.Id,
                        b.BatchNumber,
                        b.RetailPricePaisa,
                        b.CostPricePaisa,
                        b.CurrentQty,
                        b.ExpiryDate
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return NotFound();
        }

        if (product.Sellable is not null)
        {
            return Ok(new SalesProductDto
            {
                ProductId = product.Id,
                BatchId = product.Sellable.Id,
                BatchNumber = product.Sellable.BatchNumber,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                ShortCode = product.ShortCode,
                CategoryName = product.CategoryName,
                IsLoose = product.IsLoose,
                BaseUnit = product.BaseUnit,
                UnitPricePaisa = product.Sellable.RetailPricePaisa,
                AvailableStock = product.Sellable.CurrentQty,
                ExpiryDate = product.Sellable.ExpiryDate
            });
        }

        if (product.Latest is null)
        {
            return Ok(new SalesProductDto
            {
                ProductId = product.Id,
                BatchId = Guid.Empty,
                BatchNumber = string.Empty,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                ShortCode = product.ShortCode,
                CategoryName = product.CategoryName,
                IsLoose = product.IsLoose,
                BaseUnit = product.BaseUnit,
                UnitPricePaisa = 0,
                AvailableStock = 0,
                ExpiryDate = null
            });
        }

        long unitPrice = product.Latest.RetailPricePaisa > 0
            ? product.Latest.RetailPricePaisa
            : product.Latest.CostPricePaisa;

        return Ok(new SalesProductDto
        {
            ProductId = product.Id,
            BatchId = product.Latest.Id,
            BatchNumber = product.Latest.BatchNumber,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            ShortCode = product.ShortCode,
            CategoryName = product.CategoryName,
            IsLoose = product.IsLoose,
            BaseUnit = product.BaseUnit,
            UnitPricePaisa = unitPrice,
            AvailableStock = product.Latest.CurrentQty,
            ExpiryDate = product.Latest.ExpiryDate
        });
    }

    [HttpGet("for-sale")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<SalesProductDto>>> GetProductsForSale(
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        List<SalesProductDto> products = await _context.ProductBatches
            .AsNoTracking()
            .Where(batch =>
                batch.CurrentQty > 0
                && batch.RetailPricePaisa > 0
                && (batch.ExpiryDate == null || batch.ExpiryDate >= today))
            .OrderBy(batch => batch.Product.Name)
            .ThenBy(batch => batch.ExpiryDate)
            .Select(batch => new SalesProductDto
            {
                ProductId = batch.ProductId,
                BatchId = batch.Id,
                BatchNumber = batch.BatchNumber,
                Name = batch.Product.Name,
                Sku = batch.Product.Sku,
                Barcode = batch.Product.Barcode,
                ShortCode = batch.Product.ShortCode,
                CategoryName = batch.Product.Category != null ? batch.Product.Category.Name : string.Empty,
                IsLoose = batch.Product.IsLoose,
                BaseUnit = batch.Product.BaseUnit,
                UnitPricePaisa = batch.RetailPricePaisa,
                AvailableStock = batch.CurrentQty,
                ExpiryDate = batch.ExpiryDate
            }).ToListAsync(cancellationToken);

        return Ok(products);
    }

    /// <summary>
    /// Catalog for supplier intake: all non-deleted products, including zero stock.
    /// </summary>
    [HttpGet("for-receive")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<SalesProductDto>>> GetProductsForReceive(
        CancellationToken cancellationToken)
    {
        var rows = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.BaseUnit,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                AvailableStock = p.Batches.Sum(b => (decimal?)b.CurrentQty) ?? 0m,
                Latest = p.Batches
                    .OrderByDescending(b => b.CreatedAt)
                    .Select(b => new
                    {
                        b.Id,
                        b.BatchNumber,
                        b.RetailPricePaisa,
                        b.CostPricePaisa,
                        b.ExpiryDate
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        List<SalesProductDto> products = rows
            .Select(p => new SalesProductDto
            {
                ProductId = p.Id,
                BatchId = p.Latest?.Id ?? Guid.Empty,
                BatchNumber = p.Latest?.BatchNumber ?? string.Empty,
                Name = p.Name,
                Sku = p.Sku,
                Barcode = p.Barcode,
                ShortCode = p.ShortCode,
                CategoryName = p.CategoryName,
                IsLoose = p.IsLoose,
                BaseUnit = p.BaseUnit,
                // Prefer last retail; fall back to last cost so intake has a non-zero hint.
                UnitPricePaisa = p.Latest is null
                    ? 0L
                    : (p.Latest.RetailPricePaisa > 0
                        ? p.Latest.RetailPricePaisa
                        : p.Latest.CostPricePaisa),
                AvailableStock = p.AvailableStock,
                ExpiryDate = p.Latest?.ExpiryDate
            })
            .ToList();

        return Ok(products);
    }

    private static ProductDto ToCommonDto(ProductAdminDto p) =>
        new()
        {
            Id = p.Id,
            CategoryId = p.CategoryId,
            Name = p.Name,
            Sku = p.Sku,
            Barcode = p.Barcode,
            ShortCode = p.ShortCode,
            IsLoose = p.IsLoose,
            Brand = p.Brand,
            BaseUnit = p.BaseUnit,
            PurchaseUnit = p.PurchaseUnit,
            ConversionMultiplier = p.ConversionMultiplier,
            ShowOnWebshop = p.ShowOnWebshop,
            MinStockQty = p.MinStockQty
        };
}

public sealed class RenameBrandRequest
{
    public required string FromBrand { get; init; }

    public required string ToBrand { get; init; }
}

