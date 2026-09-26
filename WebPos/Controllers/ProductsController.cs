using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebPos.Core;
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

    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedProductResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedProductResult>> SearchProducts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool lowStockOnly = false,
        CancellationToken cancellationToken = default)
    {
        PagedResult<ProductAdminDto> result = await _productAdminService.SearchAsync(
            new ProductSearchQuery
            {
                Search = search,
                Page = page,
                PageSize = pageSize,
                LowStockOnly = lowStockOnly
            },
            cancellationToken);

        return Ok(new PagedProductResult
        {
            Items = result.Items.Select(ToCommonDto).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("bulk-parents")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetBulkParents(
        [FromQuery] string? search,
        [FromQuery] int take = 50,
        [FromQuery] Guid? includeId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProductAdminDto> parents = await _productAdminService.ListBulkParentsAsync(
            search,
            take,
            includeId,
            cancellationToken);
        return Ok(parents.Select(ToCommonDto).ToList());
    }

    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetProduct(
        Guid productId,
        CancellationToken cancellationToken)
    {
        try
        {
            ProductAdminDto product = await _productAdminService.GetAsync(productId, cancellationToken);
            return Ok(ToCommonDto(product));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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
        [FromBody] WebPos.Core.Abstractions.UpsertProductRequest request,
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
        [FromBody] WebPos.Core.Abstractions.UpsertProductRequest request,
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

    [HttpGet("by-barcode/{barcode}")]
    [ProducesResponseType(typeof(SalesProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SaleMasterDto), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetProductByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        string normalized = Uri.UnescapeDataString(barcode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest("Barcode is required.");
        }

        var product = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && !p.IsBulk && p.Barcode == normalized)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.BaseUnit,
                p.RetailPricePaisa,
                p.StockQty,
                p.ParentProductId,
                p.DeductionMultiplier,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                ParentStockQty = p.ParentProduct != null ? p.ParentProduct.StockQty : (decimal?)null,
                ParentBaseUnit = p.ParentProduct != null ? p.ParentProduct.BaseUnit : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product is not null)
        {
            decimal available = product.ParentProductId is Guid
                && product.DeductionMultiplier is decimal mult
                && mult > 0
                && product.ParentStockQty is decimal parentStock
                ? Math.Floor(parentStock / mult)
                : product.StockQty;

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
                PackingSize = FormatPacking(
                    product.ParentProductId,
                    product.DeductionMultiplier,
                    product.ParentBaseUnit,
                    product.BaseUnit),
                UnitPricePaisa = product.RetailPricePaisa,
                AvailableStock = available,
                ExpiryDate = null
            });
        }

        SaleMasterDto? master = await TryBuildMasterByCodeAsync(normalized, cancellationToken);
        if (master is not null)
        {
            return Conflict(master);
        }

        return NotFound();
    }

    [HttpGet("for-sale")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<SalesProductDto>>> GetProductsForSale(
        CancellationToken cancellationToken)
    {
        var rows = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && !p.IsBulk && p.RetailPricePaisa > 0)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.BaseUnit,
                p.RetailPricePaisa,
                p.StockQty,
                p.ParentProductId,
                p.DeductionMultiplier,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                ParentStockQty = p.ParentProduct != null ? p.ParentProduct.StockQty : (decimal?)null,
                ParentBaseUnit = p.ParentProduct != null ? p.ParentProduct.BaseUnit : null
            })
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        List<SalesProductDto> products = [];
        foreach (var p in rows)
        {
            decimal available;
            if (p.ParentProductId is Guid
                && p.DeductionMultiplier is decimal mult
                && mult > 0
                && p.ParentStockQty is decimal parentStock)
            {
                available = Math.Floor(parentStock / mult);
            }
            else if (p.ParentProductId is not null)
            {
                continue;
            }
            else
            {
                available = p.StockQty;
            }

            // Include zero-stock rows so search/price-check can still show catalog price.
            products.Add(new SalesProductDto
            {
                ProductId = p.Id,
                BatchId = Guid.Empty,
                BatchNumber = string.Empty,
                Name = p.Name,
                Sku = p.Sku,
                Barcode = p.Barcode,
                ShortCode = p.ShortCode,
                CategoryName = p.CategoryName,
                IsLoose = p.IsLoose,
                BaseUnit = p.BaseUnit,
                PackingSize = FormatPacking(
                    p.ParentProductId,
                    p.DeductionMultiplier,
                    p.ParentBaseUnit,
                    p.BaseUnit),
                UnitPricePaisa = p.RetailPricePaisa,
                AvailableStock = available,
                ExpiryDate = null
            });
        }

        return Ok(products);
    }

    [HttpGet("for-sale/masters")]
    [ProducesResponseType(typeof(IReadOnlyList<SaleMasterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<SaleMasterDto>>> GetSaleMasters(
        CancellationToken cancellationToken)
    {
        List<SaleMasterDto> masters = await BuildMastersAsync(cancellationToken);
        return Ok(masters.Where(m => m.Children.Count > 0).ToList());
    }

    [HttpGet("for-sale/quick-links")]
    [ProducesResponseType(typeof(IReadOnlyList<SaleQuickLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SaleQuickLinkDto>>> GetSaleQuickLinks(
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.ShowOnPosQuick)
            .Select(c => new { c.Id, c.Name, c.PosQuickSort })
            .ToListAsync(cancellationToken);

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.ShowOnPosQuick)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.IsBulk,
                p.BaseUnit,
                p.RetailPricePaisa,
                p.StockQty,
                p.ParentProductId,
                p.DeductionMultiplier,
                p.PosQuickSort,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                ParentStockQty = p.ParentProduct != null ? p.ParentProduct.StockQty : (decimal?)null,
                ParentBaseUnit = p.ParentProduct != null ? p.ParentProduct.BaseUnit : null
            })
            .ToListAsync(cancellationToken);

        List<SaleQuickLinkDto> links = [];

        foreach (var c in categories)
        {
            links.Add(new SaleQuickLinkDto
            {
                LinkType = "Category",
                TargetId = c.Id,
                Name = c.Name,
                SortOrder = c.PosQuickSort
            });
        }

        foreach (var p in products)
        {
            if (p.IsBulk && p.ParentProductId is null)
            {
                links.Add(new SaleQuickLinkDto
                {
                    LinkType = "Master",
                    TargetId = p.Id,
                    Name = p.Name,
                    SortOrder = p.PosQuickSort
                });
                continue;
            }

            if (p.IsBulk || p.RetailPricePaisa <= 0)
            {
                continue;
            }

            decimal available;
            if (p.ParentProductId is Guid
                && p.DeductionMultiplier is decimal mult
                && mult > 0
                && p.ParentStockQty is decimal parentStock)
            {
                available = Math.Floor(parentStock / mult);
            }
            else if (p.ParentProductId is not null)
            {
                continue;
            }
            else
            {
                available = p.StockQty;
            }

            links.Add(new SaleQuickLinkDto
            {
                LinkType = "Product",
                TargetId = p.Id,
                Name = p.Name,
                SortOrder = p.PosQuickSort,
                Product = new SalesProductDto
                {
                    ProductId = p.Id,
                    BatchId = Guid.Empty,
                    BatchNumber = string.Empty,
                    Name = p.Name,
                    Sku = p.Sku,
                    Barcode = p.Barcode,
                    ShortCode = p.ShortCode,
                    CategoryName = p.CategoryName,
                    IsLoose = p.IsLoose,
                    BaseUnit = p.BaseUnit,
                    PackingSize = FormatPacking(
                        p.ParentProductId,
                        p.DeductionMultiplier,
                        p.ParentBaseUnit,
                        p.BaseUnit),
                    UnitPricePaisa = p.RetailPricePaisa,
                    AvailableStock = available,
                    ExpiryDate = null
                }
            });
        }

        return Ok(links
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    [HttpGet("for-receive")]
    [ProducesResponseType(typeof(IReadOnlyList<SalesProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status426UpgradeRequired)]
    public async Task<ActionResult<IReadOnlyList<SalesProductDto>>> GetProductsForReceive(
        CancellationToken cancellationToken)
    {
        var masters = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.ParentProductId == null)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.IsBulk,
                p.BaseUnit,
                p.RetailPricePaisa,
                p.CostPricePaisa,
                p.StockQty,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .ToListAsync(cancellationToken);

        var childRows = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.ParentProductId != null)
            .Select(p => new
            {
                p.ParentProductId,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<string>> aliasesByParent = new();
        foreach (var child in childRows)
        {
            if (child.ParentProductId is not Guid parentId)
            {
                continue;
            }

            if (!aliasesByParent.TryGetValue(parentId, out List<string>? terms))
            {
                terms = [];
                aliasesByParent[parentId] = terms;
            }

            AddAliasTerm(terms, child.Name);
            AddAliasTerm(terms, child.Sku);
            AddAliasTerm(terms, child.Barcode);
            AddAliasTerm(terms, child.ShortCode);
        }

        List<SalesProductDto> products = masters.Select(p => new SalesProductDto
        {
            ProductId = p.Id,
            BatchId = Guid.Empty,
            BatchNumber = string.Empty,
            Name = p.Name,
            Sku = p.Sku,
            Barcode = p.Barcode,
            ShortCode = p.ShortCode,
            CategoryName = p.CategoryName,
            IsLoose = p.IsLoose,
            IsBulk = p.IsBulk,
            BaseUnit = p.BaseUnit,
            PackingSize = string.IsNullOrWhiteSpace(p.BaseUnit) ? string.Empty : p.BaseUnit.Trim(),
            UnitPricePaisa = p.RetailPricePaisa > 0 ? p.RetailPricePaisa : p.CostPricePaisa,
            AvailableStock = p.StockQty,
            ExpiryDate = null,
            AliasSearchTerms = aliasesByParent.TryGetValue(p.Id, out List<string>? aliases)
                ? aliases
                : []
        }).ToList();

        return Ok(products);
    }

    private async Task<SaleMasterDto?> TryBuildMasterByCodeAsync(
        string code,
        CancellationToken cancellationToken)
    {
        List<SaleMasterDto> masters = await BuildMastersAsync(cancellationToken);
        return masters.FirstOrDefault(m =>
            string.Equals(m.Barcode, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.ShortCode, code, StringComparison.OrdinalIgnoreCase)
            || string.Equals(m.Sku, code, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<SaleMasterDto>> BuildMastersAsync(CancellationToken cancellationToken)
    {
        var parents = await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.IsBulk && p.ParentProductId == null)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.BaseUnit,
                p.StockQty,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        if (parents.Count == 0)
        {
            return [];
        }

        HashSet<Guid> parentIds = parents.Select(p => p.Id).ToHashSet();
        var children = await _context.Products
            .AsNoTracking()
            .Where(p =>
                !p.IsDeleted
                && !p.IsBulk
                && p.ParentProductId != null
                && parentIds.Contains(p.ParentProductId.Value)
                && p.RetailPricePaisa > 0)
            .Select(p => new
            {
                p.Id,
                p.ParentProductId,
                p.Name,
                p.Sku,
                p.Barcode,
                p.ShortCode,
                p.IsLoose,
                p.BaseUnit,
                p.RetailPricePaisa,
                p.DeductionMultiplier,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, decimal> parentStock = parents.ToDictionary(p => p.Id, p => p.StockQty);
        Dictionary<Guid, string> parentUnit = parents.ToDictionary(p => p.Id, p => p.BaseUnit);

        Dictionary<Guid, List<SaleVariantChildDto>> childrenByParent = new();
        foreach (var child in children)
        {
            if (child.ParentProductId is not Guid parentId)
            {
                continue;
            }

            if (child.DeductionMultiplier is not decimal mult || mult <= 0)
            {
                continue;
            }

            if (!parentStock.TryGetValue(parentId, out decimal stock))
            {
                continue;
            }

            decimal available = Math.Floor(stock / mult);

            parentUnit.TryGetValue(parentId, out string? baseUnit);
            SaleVariantChildDto dto = new()
            {
                ProductId = child.Id,
                Name = child.Name,
                PackingSize = PackSizeParser.Format(mult, baseUnit),
                Barcode = child.Barcode,
                ShortCode = child.ShortCode,
                Sku = child.Sku,
                UnitPricePaisa = child.RetailPricePaisa,
                AvailableStock = available,
                IsLoose = child.IsLoose,
                BaseUnit = child.BaseUnit,
                CategoryName = child.CategoryName
            };

            if (!childrenByParent.TryGetValue(parentId, out List<SaleVariantChildDto>? list))
            {
                list = [];
                childrenByParent[parentId] = list;
            }

            list.Add(dto);
        }

        return parents.Select(p => new SaleMasterDto
        {
            ProductId = p.Id,
            Name = p.Name,
            ShortCode = p.ShortCode,
            Barcode = p.Barcode,
            Sku = p.Sku,
            BaseUnit = p.BaseUnit,
            CategoryName = p.CategoryName,
            Children = childrenByParent.TryGetValue(p.Id, out List<SaleVariantChildDto>? kids)
                ? kids.OrderBy(c => c.PackingSize).ThenBy(c => c.Name).ToList()
                : []
        }).ToList();
    }

    private static string FormatPacking(
        Guid? parentProductId,
        decimal? deductionMultiplier,
        string? parentBaseUnit,
        string baseUnit)
    {
        if (parentProductId is not null
            && deductionMultiplier is decimal mult
            && mult > 0)
        {
            return PackSizeParser.Format(mult, parentBaseUnit);
        }

        return string.IsNullOrWhiteSpace(baseUnit) ? string.Empty : baseUnit.Trim();
    }

    private static void AddAliasTerm(List<string> terms, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string term = value.Trim();
        if (!terms.Exists(existing => existing.Equals(term, StringComparison.OrdinalIgnoreCase)))
        {
            terms.Add(term);
        }
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
            ShowOnPosQuick = p.ShowOnPosQuick,
            PosQuickSort = p.PosQuickSort,
            MinStockQty = p.MinStockQty,
            StockQty = p.AvailableStock,
            CostPricePaisa = p.LatestCostPricePaisa,
            RetailPricePaisa = p.LatestRetailPricePaisa,
            IsBulk = p.IsBulk,
            DefaultMarginPercent = p.DefaultMarginPercent,
            ParentProductId = p.ParentProductId,
            DeductionMultiplier = p.DeductionMultiplier
        };
}

public sealed class RenameBrandRequest
{
    public required string FromBrand { get; init; }

    public required string ToBrand { get; init; }
}
