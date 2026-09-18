using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class ProductAdminService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    ITenantService tenantService) : IProductAdminService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));

    public async Task<IReadOnlyList<ProductAdminDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await SoftDeleteBlankNamedProductsAsync(context, cancellationToken);

        List<Product> products = await context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ParentProduct)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return products.Select(ToDto).ToList();
    }

    public async Task<PagedResult<ProductAdminDto>> SearchAsync(
        ProductSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 100);
        int page = Math.Max(query.Page, 1);
        bool useILike = IsNpgsql(context);

        IQueryable<Product> source = context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ParentProduct)
            .Where(p => !p.IsDeleted);

        source = ApplyTextSearch(source, query.Search, useILike);
        if (query.LowStockOnly)
        {
            source = ApplyLowStockFilter(source);
        }

        int total = await source.CountAsync(cancellationToken);
        List<Product> products = await source
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductAdminDto>
        {
            Items = products.Select(ToDto).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<ProductAdminDto>> ListBulkParentsAsync(
        string? search,
        int take = 50,
        Guid? includeId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        int limit = Math.Clamp(take <= 0 ? 50 : take, 1, 100);
        bool useILike = IsNpgsql(context);

        IQueryable<Product> source = context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => !p.IsDeleted && p.IsBulk && p.ParentProductId == null);

        source = ApplyTextSearch(source, search, useILike);

        List<Product> products = await source
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (includeId is Guid keepId
            && products.TrueForAll(p => p.Id != keepId))
        {
            Product? kept = await context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(
                    p => p.Id == keepId
                        && !p.IsDeleted
                        && p.IsBulk
                        && p.ParentProductId == null,
                    cancellationToken);
            if (kept is not null)
            {
                products.Insert(0, kept);
            }
        }

        return products.Select(ToDto).ToList();
    }

    public async Task<ProductAdminDto> GetAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Product product = await LoadProductAsync(context, productId, cancellationToken);
        return ToDto(product);
    }

    public Task<ProductAdminDto> CreateAsync(
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return ExecuteInOwnTransactionAsync(async (context, ct) =>
        {
            (string sku, string barcode) = ResolveIdentityFields(request);
            await EnsureIdentityAvailableAsync(context, sku, barcode, excludeProductId: null, ct);

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

            await ValidateParentAliasAsync(context, product, ct);
            context.Products.Add(product);

            if (request.OpeningRetailPricePaisa is long openingRetail)
            {
                product.RetailPricePaisa = openingRetail < 0 ? 0 : openingRetail;
            }

            await context.SaveChangesAsync(ct);
            Product created = await LoadProductAsync(context, productId, ct);
            return ToDto(created);
        }, cancellationToken);
    }

    public Task<ProductAdminDto> UpdateAsync(
        Guid productId,
        UpsertProductRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenant();

        return ExecuteInOwnTransactionAsync(async (context, ct) =>
        {
            (string sku, string barcode) = ResolveIdentityFields(request);

            Product product = await context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, ct)
                ?? throw new KeyNotFoundException("Product was not found.");

            await EnsureIdentityAvailableAsync(context, sku, barcode, excludeProductId: productId, ct);

            ApplyRequest(product, request, sku, barcode, DateTimeOffset.UtcNow);
            await ValidateParentAliasAsync(context, product, ct);

            if (request.CostPricePaisa is long cost && product.ParentProductId is null)
            {
                product.CostPricePaisa = cost < 0 ? 0 : cost;
            }

            if (request.RetailPricePaisa is long retail)
            {
                product.RetailPricePaisa = retail < 0 ? 0 : retail;
            }

            await context.SaveChangesAsync(ct);
            Product updated = await LoadProductAsync(context, productId, ct);
            return ToDto(updated);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListBrandsAsync(CancellationToken cancellationToken = default)
    {
        EnsureTenant();
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await context.Products
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

        return ExecuteInOwnTransactionAsync(async (context, ct) =>
        {
            List<Product> products = await context.Products
                .Where(p => !p.IsDeleted && p.Brand == from)
                .ToListAsync(ct);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (Product product in products)
            {
                product.Brand = to;
                product.UpdatedAt = now;
            }

            await context.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }

    private async Task<T> ExecuteInOwnTransactionAsync<T>(
        Func<WebPosDbContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        if (!context.Database.IsRelational())
        {
            T inMemoryResult = await action(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            return inMemoryResult;
        }

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            T result = await action(context, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            throw;
        }
    }

    private static async Task<Product> LoadProductAsync(
        WebPosDbContext context,
        Guid productId,
        CancellationToken cancellationToken)
    {
        Product? product = await context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.ParentProduct)
            .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken);

        return product ?? throw new KeyNotFoundException("Product was not found.");
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

    private static async Task EnsureIdentityAvailableAsync(
        WebPosDbContext context,
        string sku,
        string barcode,
        Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        bool barcodeTaken = await context.Products
            .AsNoTracking()
            .AnyAsync(
                p => !p.IsDeleted
                    && p.Barcode == barcode
                    && (excludeProductId == null || p.Id != excludeProductId),
                cancellationToken);
        if (barcodeTaken)
        {
            throw new InvalidOperationException("A product with this barcode already exists.");
        }

        bool skuTaken = await context.Products
            .AsNoTracking()
            .AnyAsync(
                p => !p.IsDeleted
                    && p.Sku == sku
                    && (excludeProductId == null || p.Id != excludeProductId),
                cancellationToken);
        if (skuTaken)
        {
            throw new InvalidOperationException("A product with this SKU already exists.");
        }
    }

    private static async Task SoftDeleteBlankNamedProductsAsync(
        WebPosDbContext context,
        CancellationToken cancellationToken)
    {
        List<Product> blanks = await context.Products
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

        await context.SaveChangesAsync(cancellationToken);
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
        product.PurchaseUnit = request.PurchaseUnit?.Trim() ?? string.Empty;
        product.ConversionMultiplier = request.ConversionMultiplier <= 0 ? 1 : request.ConversionMultiplier;
        product.ShowOnWebshop = request.ShowOnWebshop;
        product.ShowOnPosQuick = request.ShowOnPosQuick;
        product.PosQuickSort = request.PosQuickSort;
        product.MinStockQty = request.MinStockQty < 0 ? 0 : request.MinStockQty;
        if (request.ParentProductId is not null)
        {
            product.IsBulk = false;
            product.ParentProductId = request.ParentProductId;
            product.DeductionMultiplier = request.DeductionMultiplier;
            product.StockQty = 0;
        }
        else
        {
            product.IsBulk = request.IsBulk;
            product.ParentProductId = null;
            product.DeductionMultiplier = null;
            if (request.IsBulk)
            {
                product.DefaultMarginPercent = request.DefaultMarginPercent < 0
                    ? 0
                    : request.DefaultMarginPercent;
            }
        }

        if (request.RetailPricePaisa is long retailOnApply)
        {
            product.RetailPricePaisa = retailOnApply < 0 ? 0 : retailOnApply;
        }

        if (request.CostPricePaisa is long costOnApply && request.ParentProductId is null)
        {
            product.CostPricePaisa = costOnApply < 0 ? 0 : costOnApply;
        }

        product.UpdatedAt = now;
        return product;
    }

    private static async Task ValidateParentAliasAsync(
        WebPosDbContext context,
        Product product,
        CancellationToken cancellationToken)
    {
        if (product.ParentProductId is null)
        {
            if (product.DeductionMultiplier is not null)
            {
                product.DeductionMultiplier = null;
            }

            if (!product.IsBulk && product.Id != Guid.Empty)
            {
                bool hasChildren = await context.Products
                    .AsNoTracking()
                    .AnyAsync(
                        p => p.ParentProductId == product.Id && !p.IsDeleted,
                        cancellationToken);
                if (hasChildren)
                {
                    throw new InvalidOperationException(
                        "Cannot convert a bulk warehouse to independent while child variants still link to it.");
                }
            }

            return;
        }

        if (product.IsBulk)
        {
            throw new InvalidOperationException(
                "A child variant cannot be flagged as a bulk warehouse.");
        }

        if (product.ParentProductId == product.Id)
        {
            throw new InvalidOperationException("A product cannot be its own parent.");
        }

        if (product.DeductionMultiplier is not decimal mult || mult <= 0)
        {
            throw new InvalidOperationException(
                "Alias products require DeductionMultiplier greater than zero.");
        }

        Product parent = await context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == product.ParentProductId && !p.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Parent product was not found.");

        if (parent.ParentProductId is not null)
        {
            throw new InvalidOperationException(
                "Parent product cannot itself be an alias (one level only).");
        }

        if (!parent.IsBulk)
        {
            throw new InvalidOperationException(
                "Parent product must be a bulk warehouse item.");
        }
    }

    private static ProductAdminDto ToDto(Product product)
    {
        decimal available = product.ParentProductId is Guid
            && product.DeductionMultiplier is decimal mult
            && mult > 0
            && product.ParentProduct is not null
            ? Math.Floor(product.ParentProduct.StockQty / mult)
            : product.StockQty;

        return new ProductAdminDto
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
            PurchaseUnit = string.IsNullOrWhiteSpace(product.PurchaseUnit)
                ? product.BaseUnit
                : product.PurchaseUnit,
            ConversionMultiplier = product.ConversionMultiplier,
            ShowOnWebshop = product.ShowOnWebshop,
            ShowOnPosQuick = product.ShowOnPosQuick,
            PosQuickSort = product.PosQuickSort,
            MinStockQty = product.MinStockQty,
            AvailableStock = available,
            LatestCostPricePaisa = product.ParentProductId is null
                ? product.CostPricePaisa
                : (product.ParentProduct is null || product.DeductionMultiplier is not decimal m
                    ? 0L
                    : (long)Math.Round(product.ParentProduct.CostPricePaisa * m, MidpointRounding.AwayFromZero)),
            LatestRetailPricePaisa = product.RetailPricePaisa,
            IsBulk = product.IsBulk,
            DefaultMarginPercent = product.DefaultMarginPercent,
            ParentProductId = product.ParentProductId,
            ParentProductName = product.ParentProduct?.Name,
            DeductionMultiplier = product.DeductionMultiplier
        };
    }

    private static bool IsNpgsql(WebPosDbContext context) =>
        string.Equals(
            context.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);

    private static IQueryable<Product> ApplyTextSearch(
        IQueryable<Product> query,
        string? search,
        bool useILike)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        string term = search.Trim();
        if (useILike)
        {
            string pattern = "%" + EscapeLike(term) + "%";
            return query.Where(p =>
                EF.Functions.ILike(p.Name, pattern)
                || EF.Functions.ILike(p.Sku, pattern)
                || EF.Functions.ILike(p.Barcode, pattern)
                || EF.Functions.ILike(p.ShortCode, pattern)
                || EF.Functions.ILike(p.Brand, pattern));
        }

        return query.Where(p =>
            p.Name.Contains(term)
            || p.Sku.Contains(term)
            || p.Barcode.Contains(term)
            || p.ShortCode.Contains(term)
            || p.Brand.Contains(term));
    }

    private static IQueryable<Product> ApplyLowStockFilter(IQueryable<Product> query) =>
        query.Where(p =>
            ((p.ParentProductId != null
                    && p.DeductionMultiplier != null
                    && p.DeductionMultiplier > 0
                    && p.ParentProduct != null)
                ? Math.Floor(p.ParentProduct.StockQty / p.DeductionMultiplier.Value)
                : p.StockQty)
            <= p.MinStockQty);

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private void EnsureTenant()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for product admin.");
        }
    }
}
