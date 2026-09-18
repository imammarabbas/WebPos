using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class StockPositionService : IStockPositionService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly ITenantService _tenantService;

    public StockPositionService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<StockPositionSummaryDto> GetPositionAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        DateTimeOffset effectiveAsOf = asOf ?? DateTimeOffset.UtcNow;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            List<ProductRow> masters = await context.Products
                .AsNoTracking()
                .Where(p =>
                    p.TenantId == tenantId
                    && !p.IsDeleted
                    && p.ParentProductId == null)
                .Select(p => new ProductRow
                {
                    Id = p.Id,
                    Name = p.Name,
                    CategoryName = p.Category != null ? p.Category.Name : "Uncategorized",
                    StockQty = p.StockQty,
                    CostPricePaisa = p.CostPricePaisa
                })
                .ToListAsync(ct);

            Dictionary<Guid, decimal> qtyByProduct = masters.ToDictionary(p => p.Id, p => p.StockQty);

            if (asOf is not null)
            {
                await ReverseMovementsAfterAsOfAsync(context, tenantId, effectiveAsOf, qtyByProduct, ct);
            }

            List<StockProductValueDto> byProduct = masters
                .Select(p =>
                {
                    decimal qty = qtyByProduct.GetValueOrDefault(p.Id);
                    long value = (long)Math.Round(
                        qty * p.CostPricePaisa,
                        MidpointRounding.AwayFromZero);
                    return new StockProductValueDto
                    {
                        ProductId = p.Id,
                        ProductName = p.Name,
                        CategoryName = p.CategoryName,
                        ExpectedQty = qty,
                        ExpectedValuePaisa = value
                    };
                })
                .Where(p => p.ExpectedQty > 0 || p.ExpectedValuePaisa > 0)
                .OrderByDescending(p => p.ExpectedValuePaisa)
                .ToList();

            long expectedTotal = byProduct.Sum(p => p.ExpectedValuePaisa);

            StockCount? latestCount = await context.StockCounts
                .AsNoTracking()
                .Where(c => c.TenantId == tenantId && c.CountedAt <= effectiveAsOf)
                .OrderByDescending(c => c.CountedAt)
                .FirstOrDefaultAsync(ct);

            long? physicalTotal = null;
            if (latestCount is not null)
            {
                var countLines = await context.StockCountLines
                    .AsNoTracking()
                    .Where(l => l.TenantId == tenantId && l.StockCountId == latestCount.Id)
                    .Select(l => new
                    {
                        l.ProductId,
                        l.BatchId,
                        l.CountedQty
                    })
                    .ToListAsync(ct);

                var costByProduct = masters.ToDictionary(p => p.Id, p => p.CostPricePaisa);
                Dictionary<Guid, long> batchCost = await context.ProductBatches
                    .AsNoTracking()
                    .Where(b => b.TenantId == tenantId)
                    .Select(b => new { b.Id, b.CostPricePaisa })
                    .ToDictionaryAsync(b => b.Id, b => b.CostPricePaisa, ct);

                physicalTotal = countLines.Sum(line =>
                {
                    long unitCost = line.BatchId is Guid batchId && batchCost.TryGetValue(batchId, out long batchCostPaisa)
                        ? batchCostPaisa
                        : costByProduct.GetValueOrDefault(line.ProductId);
                    return (long)Math.Round(line.CountedQty * unitCost, MidpointRounding.AwayFromZero);
                });

                var physicalByProduct = countLines
                    .GroupBy(l => l.ProductId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.CountedQty));

                byProduct = byProduct.Select(p =>
                {
                    decimal? physQty = physicalByProduct.TryGetValue(p.ProductId, out decimal qty)
                        ? qty
                        : null;
                    long? physValue = physQty is decimal pq
                        ? (long)Math.Round(
                            pq * (p.ExpectedQty > 0
                                ? p.ExpectedValuePaisa / p.ExpectedQty
                                : costByProduct.GetValueOrDefault(p.ProductId)),
                            MidpointRounding.AwayFromZero)
                        : null;
                    return new StockProductValueDto
                    {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        CategoryName = p.CategoryName,
                        ExpectedQty = p.ExpectedQty,
                        ExpectedValuePaisa = p.ExpectedValuePaisa,
                        PhysicalQty = physQty,
                        PhysicalValuePaisa = physValue
                    };
                }).ToList();
            }

            var byCategory = byProduct
                .GroupBy(p => p.CategoryName ?? "Uncategorized")
                .Select(g => new StockCategoryValueDto
                {
                    CategoryName = g.Key,
                    ExpectedValuePaisa = g.Sum(x => x.ExpectedValuePaisa),
                    PhysicalValuePaisa = g.Any(x => x.PhysicalValuePaisa is not null)
                        ? g.Sum(x => x.PhysicalValuePaisa ?? 0)
                        : null
                })
                .OrderByDescending(c => c.ExpectedValuePaisa)
                .ToList();

            return new StockPositionSummaryDto
            {
                ExpectedValuePaisa = expectedTotal,
                PhysicalValuePaisa = physicalTotal,
                ByProduct = byProduct,
                ByCategory = byCategory,
                LastPhysicalCountAt = latestCount?.CountedAt
            };
        }, cancellationToken);
    }

    public Task<Guid> RecordPhysicalCountAsync(
        RecordStockCountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        if (request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one count line is required.");
        }

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            StockCount count = new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CountedAt = request.CountedAt ?? DateTimeOffset.UtcNow,
                Note = request.Note?.Trim(),
                CountedByUserId = request.CountedByUserId
            };
            context.StockCounts.Add(count);

            foreach (RecordStockCountLineRequest line in request.Lines)
            {
                if (line.CountedQty < 0)
                {
                    throw new ArgumentException("Counted quantity cannot be negative.");
                }

                context.StockCountLines.Add(new StockCountLine
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StockCountId = count.Id,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    CountedQty = line.CountedQty
                });
            }

            await context.SaveChangesAsync(ct);
            return count.Id;
        }, cancellationToken);
    }

    /// <summary>
    /// Walk current master <see cref="Product.StockQty"/> backward by reversing movements after <paramref name="asOf"/>.
    /// </summary>
    private static async Task ReverseMovementsAfterAsOfAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset asOf,
        Dictionary<Guid, decimal> qtyByProduct,
        CancellationToken cancellationToken)
    {
        var salesAfter = await (
            from si in context.SalesItems.AsNoTracking()
            join inv in context.SalesInvoices.AsNoTracking() on si.InvoiceNo equals inv.InvoiceNo
            join prod in context.Products.AsNoTracking() on si.ProductId equals prod.Id
            where inv.TenantId == tenantId && inv.CreatedAt > asOf
            select new
            {
                si.ProductId,
                si.Quantity,
                si.ParentQtyDeducted,
                prod.ParentProductId
            }).ToListAsync(cancellationToken);

        foreach (var sale in salesAfter)
        {
            Guid poolId = sale.ParentQtyDeducted > 0
                ? sale.ParentProductId ?? sale.ProductId
                : sale.ProductId;
            decimal restore = sale.ParentQtyDeducted > 0 ? sale.ParentQtyDeducted : sale.Quantity;
            qtyByProduct[poolId] = qtyByProduct.GetValueOrDefault(poolId) + restore;
        }

        var returnRows = await (
            from ri in context.SalesReturnItems.AsNoTracking()
            join r in context.SalesReturns.AsNoTracking() on ri.SalesReturnId equals r.Id
            where r.TenantId == tenantId && r.CreatedAt > asOf
            select new
            {
                r.OriginalInvoiceNo,
                ri.ProductId,
                ri.Quantity
            }).ToListAsync(cancellationToken);

        if (returnRows.Count > 0)
        {
            var soldLookup = await context.SalesItems
                .AsNoTracking()
                .Where(si => returnRows.Select(r => r.OriginalInvoiceNo).Contains(si.InvoiceNo))
                .Select(si => new
                {
                    si.InvoiceNo,
                    si.ProductId,
                    si.Quantity,
                    si.ParentQtyDeducted
                })
                .ToListAsync(cancellationToken);

            Dictionary<(string Invoice, Guid Product), (decimal SoldQty, decimal ParentDeducted)> soldByKey =
                soldLookup.ToDictionary(
                    x => (x.InvoiceNo, x.ProductId),
                    x => (x.Quantity, x.ParentQtyDeducted));

            Dictionary<Guid, Guid?> parentByProduct = await context.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId)
                .Select(p => new { p.Id, p.ParentProductId })
                .ToDictionaryAsync(p => p.Id, p => p.ParentProductId, cancellationToken);

            foreach (var ret in returnRows)
            {
                if (!soldByKey.TryGetValue((ret.OriginalInvoiceNo, ret.ProductId), out var sold))
                {
                    continue;
                }

                decimal restoreQty = sold.ParentDeducted > 0
                    ? sold.ParentDeducted * (ret.Quantity / sold.SoldQty)
                    : ret.Quantity;
                Guid poolId = sold.ParentDeducted > 0
                    ? parentByProduct.GetValueOrDefault(ret.ProductId) ?? ret.ProductId
                    : ret.ProductId;
                qtyByProduct[poolId] = qtyByProduct.GetValueOrDefault(poolId) - restoreQty;
            }
        }

        var receivesAfter = await context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.CreatedAt > asOf && b.InitialQty > 0)
            .Select(b => new { b.ProductId, b.InitialQty })
            .ToListAsync(cancellationToken);

        foreach (var receive in receivesAfter)
        {
            qtyByProduct[receive.ProductId] = qtyByProduct.GetValueOrDefault(receive.ProductId) - receive.InitialQty;
        }

        var purchaseReturnsAfter = await (
            from pri in context.PurchaseReturnItems.AsNoTracking()
            join pr in context.PurchaseReturns.AsNoTracking() on pri.PurchaseReturnId equals pr.Id
            where pr.TenantId == tenantId && pr.CreatedAt > asOf
            select new { pri.ProductId, pri.Quantity }).ToListAsync(cancellationToken);

        foreach (var pr in purchaseReturnsAfter)
        {
            qtyByProduct[pr.ProductId] = qtyByProduct.GetValueOrDefault(pr.ProductId) + pr.Quantity;
        }
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is not resolved.");
        }
    }

    private sealed class ProductRow
    {
        public required Guid Id { get; init; }

        public required string Name { get; init; }

        public required string CategoryName { get; init; }

        public decimal StockQty { get; init; }

        public long CostPricePaisa { get; init; }
    }
}
