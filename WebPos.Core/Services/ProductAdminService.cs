using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class ProductAdminService(
    WebPosDbContext context,
    ITenantService tenantService,
    ITransactionService transactionService) : IProductAdminService
{
    private readonly WebPosDbContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    private readonly ITransactionService _transactionService =
        transactionService ?? throw new ArgumentNullException(nameof(transactionService));

    public async Task<IReadOnlyList<ProductAdminDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await SoftDeleteBlankNamedProductsAsync(cancellationToken);

        List<Product> products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return products.Select(ToDto).ToList();
    }

    public async Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        Product product = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Batches)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Product was not found.");

        return ToDto(product);
    }

    public Task<ProductAdminDto> CreateAsync(
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            (string sku, string barcode) = ResolveIdentityFields(request);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid productId = Guid.NewGuid();
            Product product = ApplyRequest(new Product
            {
                Id = productId,
                TenantId = _tenantService.TenantId,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false,
                ConversionMultiplier = request.ConversionMultiplier <= 0 ? 1 : request.ConversionMultiplier
            }, request, sku, barcode, now);

            _context.Products.Add(product);

            if (request.OpeningStockQty is > 0 && request.OpeningRetailPricePaisa is > 0)
            {
                Guid? supplierId = await _context.Parties
                    .AsNoTracking()
                    .Where(p => p.PartyType == "SUPPLIER")
                    .Select(p => (Guid?)p.Id)
                    .FirstOrDefaultAsync(ct);

                if (supplierId is Guid sid)
                {
                    _context.ProductBatches.Add(new ProductBatch
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantService.TenantId,
                        ProductId = productId,
                        BatchNumber = $"OPEN-{productId:N}"[..20],
                        ExpiryDate = null,
                        CostPricePaisa = request.OpeningCostPricePaisa ?? 0,
                        RetailPricePaisa = request.OpeningRetailPricePaisa.Value,
                        InitialQty = request.OpeningStockQty.Value,
                        CurrentQty = request.OpeningStockQty.Value,
                        SupplierId = sid,
                        RackLocation = "OPEN",
                        CreatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync(ct);
            return await GetAsync(productId, ct);
        }, cancellationToken);
    }

    public Task<ProductAdminDto> UpdateAsync(
        Guid productId,
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            (string sku, string barcode) = ResolveIdentityFields(request);

            Product product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Product was not found.");

            ApplyRequest(product, request, sku, barcode, DateTimeOffset.UtcNow);
            await _context.SaveChangesAsync(ct);
            return await GetAsync(productId, ct);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListBrandsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        return await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Brand != string.Empty)
            .Select(p => p.Brand)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(cancellationToken);
    }

    public Task RenameBrandAsync(
        string fromBrand,
        string toBrand,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromBrand) || string.IsNullOrWhiteSpace(toBrand))
        {
            throw new ArgumentException("Brand names are required.");
        }

        EnsureTenant();
        string from = fromBrand.Trim();
        string to = toBrand.Trim();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            List<Product> products = await _context.Products
                .Where(p => !p.IsDeleted && p.Brand == from)
                .ToListAsync(ct);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (Product product in products)
            {
                product.Brand = to;
                product.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static (string Sku, string Barcode) ResolveIdentityFields(UpsertProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Product name is required.");
        }

        string sku = string.IsNullOrWhiteSpace(request.Sku)
            ? $"SKU-{Guid.NewGuid():N}"[..16]
            : request.Sku.Trim();

        string barcode = string.IsNullOrWhiteSpace(request.Barcode)
            || string.Equals(request.Barcode.Trim(), "NO BARCODE", StringComparison.OrdinalIgnoreCase)
            ? $"NB-{Guid.NewGuid():N}"[..20]
            : request.Barcode.Trim();

        return (sku, barcode);
    }

    private async Task SoftDeleteBlankNamedProductsAsync(CancellationToken cancellationToken)
    {
        List<Product> blanks = await _context.Products
            .Where(p => !p.IsDeleted && p.Name == string.Empty)
            .ToListAsync(cancellationToken);
        if (blanks.Count == 0)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (Product blank in blanks)
        {
            blank.IsDeleted = true;
            blank.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Product ApplyRequest(
        Product product,
        UpsertProductRequest request,
        string sku,
        string barcode,
        DateTimeOffset now)
    {
        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.Sku = sku;
        product.Barcode = barcode;
        product.ShortCode = request.ShortCode?.Trim() ?? string.Empty;
        product.IsLoose = request.IsLoose;
        product.Brand = request.Brand?.Trim() ?? string.Empty;
        product.BaseUnit = string.IsNullOrWhiteSpace(request.BaseUnit) ? "PCS" : request.BaseUnit.Trim();
        product.ConversionMultiplier = request.ConversionMultiplier <= 0 ? 1 : request.ConversionMultiplier;
        product.ShowOnWebshop = request.ShowOnWebshop;
        product.MinStockQty = request.MinStockQty < 0 ? 0 : request.MinStockQty;
        product.UpdatedAt = now;
        return product;
    }

    private static ProductAdminDto ToDto(Product product) =>
        new()
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name,
            Name = product.Name,
            Sku = product.Sku,
            Barcode = product.Barcode,
            ShortCode = product.ShortCode,
            IsLoose = product.IsLoose,
            Brand = product.Brand,
            BaseUnit = product.BaseUnit,
            ConversionMultiplier = product.ConversionMultiplier,
            ShowOnWebshop = product.ShowOnWebshop,
            MinStockQty = product.MinStockQty,
            AvailableStock = product.Batches.Sum(b => b.CurrentQty)
        };

    private void EnsureTenant()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for product admin.");
        }
    }
}

