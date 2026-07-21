using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Filters;

namespace WebPos.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
[TenantAuthorize]
public sealed class ProductsController : ControllerBase
{
    private readonly WebPosDbContext _context;

    public ProductsController(WebPosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        List<ProductDto> products = await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                CategoryId = p.CategoryId,
                Name = p.Name,
                Sku = p.Sku,
                Barcode = p.Barcode,
                Brand = p.Brand,
                BaseUnit = p.BaseUnit,
                ConversionMultiplier = p.ConversionMultiplier,
                ShowOnWebshop = p.ShowOnWebshop
            })
            .ToListAsync(cancellationToken);

        return Ok(products);
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
                UnitPricePaisa = batch.RetailPricePaisa,
                AvailableStock = batch.CurrentQty,
                ExpiryDate = batch.ExpiryDate
            })            .ToListAsync(cancellationToken);

        return Ok(products);
    }
}
