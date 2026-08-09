using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class MasterOverviewService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    ITenantService tenantService,
    IStoreStatusService storeStatusService) : IMasterOverviewService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    private readonly IStoreStatusService _storeStatusService =
        storeStatusService ?? throw new ArgumentNullException(nameof(storeStatusService));

    public async Task<MasterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for overview.");
        }

        OverviewSeed overviewSeed;
        await using (WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken))
        {
            List<Product> products = await context.Products
                .AsNoTracking()
                .Include(p => p.Batches)
                .Include(p => p.Category)
                .Where(p => !p.IsDeleted)
                .ToListAsync(cancellationToken);

            int customerCount = await context.Parties.CountAsync(
                p => p.PartyType == PartyTypes.Customer, cancellationToken);
            int supplierCount = await context.Parties.CountAsync(
                p => p.PartyType == PartyTypes.Supplier, cancellationToken);

            List<LowStockItemDto> lowStock = products
                .Select(p => new
                {
                    Product = p,
                    Stock = p.Batches.Sum(b => b.CurrentQty)
                })
                .Where(x => x.Stock < x.Product.MinStockQty)
                .OrderBy(x => x.Stock)
                .Take(10)
                .Select(x => new LowStockItemDto
                {
                    ProductId = x.Product.Id,
                    Name = x.Product.Name,
                    AvailableStock = x.Stock,
                    MinStockQty = x.Product.MinStockQty
                })
                .ToList();

            int totalProducts = products.Count;
            List<CategoryStockDto> byCategory = products
                .GroupBy(p => p.Category?.Name ?? "Uncategorized")
                .Select(g => new CategoryStockDto
                {
                    CategoryName = g.Key,
                    ProductCount = g.Count(),
                    Percent = totalProducts == 0 ? 0 : (int)Math.Round(100.0 * g.Count() / totalProducts)
                })
                .OrderByDescending(c => c.ProductCount)
                .ToList();

            DateTimeOffset dayStart = DateTimeOffset.UtcNow.Date;
            DateTimeOffset dayEnd = dayStart.AddDays(1);
            var todaySales = await context.SalesInvoices
                .AsNoTracking()
                .Where(i => i.CreatedAt >= dayStart && i.CreatedAt < dayEnd)
                .Select(i => i.TotalAmountPaisa)
                .ToListAsync(cancellationToken);

            List<ActivityItemDto> activity = await BuildActivityAsync(context, cancellationToken);

            overviewSeed = new OverviewSeed(
                totalProducts,
                customerCount,
                supplierCount,
                lowStock,
                byCategory,
                todaySales.Sum(),
                todaySales.Count,
                activity);
        }

        StoreStatusDto storeStatus = await _storeStatusService.GetStatusAsync(cancellationToken);

        return new MasterOverviewDto
        {
            ProductCount = overviewSeed.ProductCount,
            CustomerCount = overviewSeed.CustomerCount,
            SupplierCount = overviewSeed.SupplierCount,
            LowStockCount = overviewSeed.LowStock.Count,
            TodayRevenuePaisa = overviewSeed.TodayRevenuePaisa,
            TodayOrderCount = overviewSeed.TodayOrderCount,
            TillOpen = storeStatus.TillOpen,
            ApiOnline = storeStatus.ApiOnline,
            LowStockItems = overviewSeed.LowStock,
            InventoryByCategory = overviewSeed.ByCategory,
            RecentActivity = overviewSeed.Activity
        };
    }

    private sealed record OverviewSeed(
        int ProductCount,
        int CustomerCount,
        int SupplierCount,
        List<LowStockItemDto> LowStock,
        List<CategoryStockDto> ByCategory,
        long TodayRevenuePaisa,
        int TodayOrderCount,
        List<ActivityItemDto> Activity);

    private static async Task<List<ActivityItemDto>> BuildActivityAsync(
        WebPosDbContext context,
        CancellationToken cancellationToken)
    {
        List<ActivityItemDto> items = [];

        items.AddRange(await context.Products.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(5)
            .Select(p => new ActivityItemDto
            {
                Kind = "product",
                Title = "Product updated",
                Detail = p.Name + (p.IsLoose ? " · Loose" : string.Empty),
                At = p.UpdatedAt
            })
            .ToListAsync(cancellationToken));

        items.AddRange(await context.Parties.AsNoTracking()
            .OrderByDescending(p => p.UpdatedAt)
            .Take(5)
            .Select(p => new ActivityItemDto
            {
                Kind = p.PartyType == PartyTypes.Customer ? "customer" : "supplier",
                Title = p.PartyType == PartyTypes.Customer ? "Customer updated" : "Supplier updated",
                Detail = p.Name + " · " + p.PhoneNumber,
                At = p.UpdatedAt
            })
            .ToListAsync(cancellationToken));

        items.AddRange(await context.Categories.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(3)
            .Select(c => new ActivityItemDto
            {
                Kind = "category",
                Title = "Category created",
                Detail = c.Name,
                At = c.CreatedAt
            })
            .ToListAsync(cancellationToken));

        return items
            .OrderByDescending(a => a.At)
            .Take(8)
            .ToList();
    }
}
