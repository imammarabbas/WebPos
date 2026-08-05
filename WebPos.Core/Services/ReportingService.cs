using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class ReportingService : IReportingService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly ITenantService _tenantService;

    public ReportingService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public async Task<IReadOnlyList<ExpenseCategorySummary>> GetExpenseSummaryByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await context.ShiftExpenses
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.LoggedAt >= from
                && e.LoggedAt <= to)
            .GroupBy(e => e.ExpenseCategory)
            .Select(g => new ExpenseCategorySummary
            {
                ExpenseCategory = g.Key,
                TotalAmountPaisa = g.Sum(x => x.AmountPaisa),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmountPaisa)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetMaintenanceSpendAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Ledger is the SSOT for financial totals. ShiftExpenses is operational detail only;
        // Math.Max previously masked divergence between the two sources.
        string maintenanceAccount = LedgerAccounts.Expense(ExpenseCategories.Maintenance);

        return await context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.AccountCode == maintenanceAccount
                && e.CreatedAt >= from
                && e.CreatedAt <= to)
            .SumAsync(e => e.DebitPaisa, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductProfitSummary>> GetGrossProfitByProductAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Aggregate in SQL; round on the client (Npgsql cannot translate Math.Round MidpointRounding).
        var aggregates = await context.SalesItems
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.Invoice.TenantId == tenantId
                && i.Invoice.CreatedAt >= from
                && i.Invoice.CreatedAt <= to)
            .GroupBy(i => new { i.ProductId, ProductName = i.Product.Name })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                Revenue = g.Sum(x => x.Quantity * x.UnitPricePaisa),
                Cost = g.Sum(x => x.Quantity * x.UnitCostPaisa)
            })
            .ToListAsync(cancellationToken);

        return aggregates
            .Select(g => new ProductProfitSummary
            {
                ProductId = g.ProductId,
                ProductName = g.ProductName,
                RevenuePaisa = (long)Math.Round(g.Revenue, MidpointRounding.AwayFromZero),
                CostPaisa = (long)Math.Round(g.Cost, MidpointRounding.AwayFromZero)
            })
            .OrderByDescending(x => x.GrossProfitPaisa)
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryProfitSummary>> GetGrossProfitByCategoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var aggregates = await context.SalesItems
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.Invoice.TenantId == tenantId
                && i.Product.CategoryId != null
                && i.Invoice.CreatedAt >= from
                && i.Invoice.CreatedAt <= to)
            .GroupBy(i => new
            {
                CategoryId = i.Product.CategoryId!.Value,
                CategoryName = i.Product.Category!.Name
            })
            .Select(g => new
            {
                g.Key.CategoryId,
                g.Key.CategoryName,
                Revenue = g.Sum(x => x.Quantity * x.UnitPricePaisa),
                Cost = g.Sum(x => x.Quantity * x.UnitCostPaisa)
            })
            .ToListAsync(cancellationToken);

        return aggregates
            .Select(g => new CategoryProfitSummary
            {
                CategoryId = g.CategoryId,
                CategoryName = g.CategoryName,
                RevenuePaisa = (long)Math.Round(g.Revenue, MidpointRounding.AwayFromZero),
                CostPaisa = (long)Math.Round(g.Cost, MidpointRounding.AwayFromZero)
            })
            .OrderByDescending(x => x.GrossProfitPaisa)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountCashFlowSummary>> GetCashFlowByAccountAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        return await context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.CreatedAt >= from
                && e.CreatedAt <= to)
            .GroupBy(e => e.AccountCode)
            .Select(g => new AccountCashFlowSummary
            {
                AccountCode = g.Key,
                DebitTotalPaisa = g.Sum(x => x.DebitPaisa),
                CreditTotalPaisa = g.Sum(x => x.CreditPaisa)
            })
            .OrderBy(x => x.AccountCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProfitAndLossSummary> GetProfitAndLossAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProductProfitSummary> products =
            await GetGrossProfitByProductAsync(from, to, cancellationToken);
        IReadOnlyList<CategoryProfitSummary> categories =
            await GetGrossProfitByCategoryAsync(from, to, cancellationToken);
        IReadOnlyList<ExpenseCategorySummary> allExpenses =
            await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        List<ExpenseCategorySummary> operating = allExpenses
            .Where(e => ExpenseCategories.IsOperatingOverhead(e.ExpenseCategory))
            .ToList();

        long revenue = products.Sum(p => p.RevenuePaisa);
        long cost = products.Sum(p => p.CostPaisa);
        long overhead = operating.Sum(e => e.TotalAmountPaisa);

        return new ProfitAndLossSummary
        {
            From = from,
            To = to,
            RevenuePaisa = revenue,
            CostPaisa = cost,
            OperatingExpensesPaisa = overhead,
            ExpensesByCategory = operating,
            ProductProfits = products,
            CategoryProfits = categories
        };
    }

    public async Task<BiDashboardResult> GetBiDashboardAsync(
        BiDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.To < query.From)
        {
            throw new ArgumentException("Query 'To' must be on or after 'From'.", nameof(query));
        }

        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        DateTimeOffset from = query.From;
        DateTimeOffset to = query.To;

        TimeSpan span = to - from;
        DateTimeOffset prevTo = from.AddTicks(-1);
        DateTimeOffset prevFrom = prevTo - span;

        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        List<LineAgg> currentLines = await LoadLineAggregatesAsync(
            context, tenantId, from, to, query, cancellationToken);
        List<LineAgg> priorLines = query.ComparePrevious
            ? await LoadLineAggregatesAsync(context, tenantId, prevFrom, prevTo, query, cancellationToken)
            : [];

        List<InvoiceAgg> currentInvoices = await LoadInvoiceAggregatesAsync(
            context, tenantId, from, to, query, cancellationToken);
        List<InvoiceAgg> priorInvoices = query.ComparePrevious
            ? await LoadInvoiceAggregatesAsync(context, tenantId, prevFrom, prevTo, query, cancellationToken)
            : [];

        Dictionary<Guid, decimal> stockByProduct = await context.ProductBatches
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId && b.CurrentQty > 0)
            .GroupBy(b => b.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.CurrentQty) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Qty, cancellationToken);

        Dictionary<Guid, string> terminalNames = await context.Terminals
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .ToDictionaryAsync(t => t.Id, t => t.TerminalName, cancellationToken);

        // Seasonality: last 6 calendar months (unfiltered by category/terminal for shop-wide context,
        // but still tenant-scoped; apply same optional filters for consistency).
        DateTimeOffset seasonEnd = to;
        DateTimeOffset seasonStart = new DateTimeOffset(
            new DateOnly(to.Year, to.Month, 1).AddMonths(-5),
            TimeOnly.MinValue,
            TimeSpan.Zero);
        List<DailyBucket> seasonDaily = await LoadDailyBucketsAsync(
            context, tenantId, seasonStart, seasonEnd, query, cancellationToken);

        List<DailyBucket> daily = await LoadDailyBucketsAsync(
            context, tenantId, from, to, query, cancellationToken);

        List<HeatRaw> heatRaw = await LoadHeatmapAsync(
            context, tenantId, from, to, query, cancellationToken);

        // --- KPIs ---
        long sales = currentLines.Sum(x => x.RevenuePaisa);
        long cost = currentLines.Sum(x => x.CostPaisa);
        long profit = sales - cost;
        decimal itemsSold = currentLines.Sum(x => x.Quantity);
        int tx = currentInvoices.Count;
        long avgBasket = tx == 0 ? 0 : (long)Math.Round((decimal)sales / tx, MidpointRounding.AwayFromZero);
        decimal margin = Pct(profit, sales);

        long priorSales = priorLines.Sum(x => x.RevenuePaisa);
        long priorCost = priorLines.Sum(x => x.CostPaisa);
        long priorProfit = priorSales - priorCost;
        decimal priorItems = priorLines.Sum(x => x.Quantity);
        int priorTx = priorInvoices.Count;
        long priorBasket = priorTx == 0
            ? 0
            : (long)Math.Round((decimal)priorSales / priorTx, MidpointRounding.AwayFromZero);
        decimal priorMargin = Pct(priorProfit, priorSales);

        List<long> salesSpark = SampleSparkline(daily.Select(d => d.SalesPaisa).ToList(), 14);
        List<long> profitSpark = SampleSparkline(daily.Select(d => d.ProfitPaisa).ToList(), 14);

        var kpis = new BiKpis
        {
            SalesPaisa = sales,
            GrossProfitPaisa = profit,
            MarginPercent = margin,
            ItemsSold = itemsSold,
            AvgBasketPaisa = avgBasket,
            Transactions = tx,
            SalesDeltaPercent = query.ComparePrevious ? DeltaPct(sales, priorSales) : null,
            GrossProfitDeltaPercent = query.ComparePrevious ? DeltaPct(profit, priorProfit) : null,
            MarginDeltaPercent = query.ComparePrevious ? margin - priorMargin : null,
            ItemsSoldDeltaPercent = query.ComparePrevious ? DeltaPct(itemsSold, priorItems) : null,
            AvgBasketDeltaPercent = query.ComparePrevious ? DeltaPct(avgBasket, priorBasket) : null,
            TransactionsDeltaPercent = query.ComparePrevious ? DeltaPct(tx, priorTx) : null,
            SalesSparklinePaisa = salesSpark,
            ProfitSparklinePaisa = profitSpark
        };

        // --- Categories ---
        Dictionary<(Guid Id, string Name), (long Rev, long Cost)> priorCat = priorLines
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => (x.CategoryId!.Value, x.CategoryName ?? "Uncategorized"))
            .ToDictionary(g => g.Key, g => (g.Sum(x => x.RevenuePaisa), g.Sum(x => x.CostPaisa)));

        List<BiCategoryRow> categories = currentLines
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => (x.CategoryId!.Value, x.CategoryName ?? "Uncategorized"))
            .Select(g =>
            {
                long rev = g.Sum(x => x.RevenuePaisa);
                long cst = g.Sum(x => x.CostPaisa);
                long gp = rev - cst;
                priorCat.TryGetValue(g.Key, out var prev);
                long prevGp = prev.Rev - prev.Cost;
                return new BiCategoryRow
                {
                    CategoryId = g.Key.Item1,
                    CategoryName = g.Key.Item2,
                    SalesPaisa = rev,
                    ProfitPaisa = gp,
                    MarginPercent = Pct(gp, rev),
                    PercentOfTotal = sales == 0 ? 0m : Math.Round(rev * 100m / sales, 1, MidpointRounding.AwayFromZero),
                    TrendPercent = query.ComparePrevious ? DeltaPct(gp, prevGp) : null
                };
            })
            .OrderByDescending(c => c.SalesPaisa)
            .ToList();

        List<BiShareSlice> categoryShare = categories
            .Where(c => c.ProfitPaisa > 0)
            .Select(c => new BiShareSlice
            {
                Key = c.CategoryId.ToString(),
                Label = c.CategoryName,
                AmountPaisa = c.ProfitPaisa,
                PercentOfTotal = profit <= 0
                    ? 0m
                    : Math.Round(c.ProfitPaisa * 100m / profit, 1, MidpointRounding.AwayFromZero),
                Count = 0
            })
            .OrderByDescending(s => s.AmountPaisa)
            .ToList();
        NormalizePercents(categoryShare);

        // --- Products / SKUs ---
        Dictionary<Guid, long> priorProductSales = priorLines
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.RevenuePaisa));

        var productGroups = currentLines
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductName,
                x.CategoryId,
                CategoryName = x.CategoryName ?? "Uncategorized"
            })
            .Select(g =>
            {
                long rev = g.Sum(x => x.RevenuePaisa);
                long cst = g.Sum(x => x.CostPaisa);
                long gp = rev - cst;
                decimal qty = g.Sum(x => x.Quantity);
                priorProductSales.TryGetValue(g.Key.ProductId, out long prevRev);
                decimal? growth = query.ComparePrevious ? DeltaPct(rev, prevRev) : null;
                return new
                {
                    g.Key.ProductId,
                    g.Key.ProductName,
                    g.Key.CategoryId,
                    g.Key.CategoryName,
                    Quantity = qty,
                    Sales = rev,
                    Profit = gp,
                    Margin = Pct(gp, rev),
                    Growth = growth
                };
            })
            .ToList();

        // Dead stock: on-hand with zero sales in range (include products not in currentLines)
        HashSet<Guid> soldIds = currentLines.Select(x => x.ProductId).ToHashSet();
        List<(Guid ProductId, string Name, Guid? CatId, string CatName, decimal Qty)> deadExtras = [];
        if (stockByProduct.Count > 0)
        {
            List<Guid> deadIds = stockByProduct.Keys.Where(id => !soldIds.Contains(id)).Take(50).ToList();
            if (deadIds.Count > 0)
            {
                var deadProducts = await context.Products
                    .AsNoTracking()
                    .Where(p => p.TenantId == tenantId && deadIds.Contains(p.Id))
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.CategoryId,
                        CategoryName = p.Category != null ? p.Category.Name : "Uncategorized"
                    })
                    .ToListAsync(cancellationToken);

                foreach (var p in deadProducts)
                {
                    if (query.CategoryId is Guid catFilter && p.CategoryId != catFilter)
                    {
                        continue;
                    }

                    deadExtras.Add((
                        p.Id,
                        p.Name,
                        p.CategoryId,
                        p.CategoryName ?? "Uncategorized",
                        stockByProduct.GetValueOrDefault(p.Id)));
                }
            }
        }

        List<(long Sales, long Profit)> mediansSource = productGroups
            .Select(p => (p.Sales, p.Profit))
            .ToList();
        long medianSales = Median(mediansSource.Select(x => x.Sales).ToList());
        long medianProfit = Median(mediansSource.Select(x => x.Profit).ToList());

        List<BiProductRow> products = productGroups
            .Select(p =>
            {
                bool highSales = p.Sales >= medianSales;
                bool highProfit = p.Profit >= medianProfit;
                string quadrant = (highSales, highProfit) switch
                {
                    (true, true) => BiQuadrants.Stars,
                    (true, false) => BiQuadrants.Workhorses,
                    (false, true) => BiQuadrants.Opportunities,
                    _ => BiQuadrants.Dogs
                };
                bool declining = p.Growth is <= -20m;
                bool lowMargin = p.Margin < 15m && p.Sales > 0;
                return new BiProductRow
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryId = p.CategoryId,
                    CategoryName = p.CategoryName,
                    Quantity = p.Quantity,
                    SalesPaisa = p.Sales,
                    ProfitPaisa = p.Profit,
                    MarginPercent = p.Margin,
                    GrowthPercent = p.Growth,
                    Quadrant = quadrant,
                    IsDeclining = declining,
                    IsLowMargin = lowMargin,
                    IsDeadStock = false
                };
            })
            .Concat(deadExtras.Select(d => new BiProductRow
            {
                ProductId = d.ProductId,
                ProductName = d.Name,
                CategoryId = d.CatId,
                CategoryName = d.CatName,
                Quantity = d.Qty,
                SalesPaisa = 0,
                ProfitPaisa = 0,
                MarginPercent = 0,
                GrowthPercent = query.ComparePrevious ? -100m : null,
                Quadrant = BiQuadrants.Dogs,
                IsDeclining = false,
                IsLowMargin = false,
                IsDeadStock = true
            }))
            .OrderByDescending(p => p.SalesPaisa)
            .ToList();

        List<BiSkuRow> topSkus = productGroups
            .OrderByDescending(p => p.Profit)
            .Take(10)
            .Select(p => new BiSkuRow
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                TotalProfitPaisa = p.Profit,
                PerUnitProfitPaisa = p.Quantity == 0
                    ? 0
                    : (long)Math.Round(p.Profit / p.Quantity, MidpointRounding.AwayFromZero),
                Quantity = p.Quantity
            })
            .ToList();

        // --- Terminals / Payments ---
        long invoiceSalesTotal = currentInvoices.Sum(i => i.TotalAmountPaisa);
        List<BiShareSlice> terminals = currentInvoices
            .GroupBy(i => i.TerminalId)
            .Select(g =>
            {
                Guid? tid = g.Key;
                string label = tid is Guid id && terminalNames.TryGetValue(id, out string? name)
                    ? name
                    : "Unassigned";
                long amt = g.Sum(x => x.TotalAmountPaisa);
                return new BiShareSlice
                {
                    Key = tid?.ToString() ?? "none",
                    Label = label,
                    AmountPaisa = amt,
                    PercentOfTotal = invoiceSalesTotal == 0
                        ? 0m
                        : Math.Round(amt * 100m / invoiceSalesTotal, 1, MidpointRounding.AwayFromZero),
                    Count = g.Count()
                };
            })
            .OrderByDescending(t => t.AmountPaisa)
            .ToList();

        List<BiShareSlice> payments = currentInvoices
            .GroupBy(i => string.IsNullOrWhiteSpace(i.PaymentMethod) ? "UNKNOWN" : i.PaymentMethod)
            .Select(g =>
            {
                long amt = g.Sum(x => x.TotalAmountPaisa);
                return new BiShareSlice
                {
                    Key = g.Key,
                    Label = g.Key,
                    AmountPaisa = amt,
                    PercentOfTotal = invoiceSalesTotal == 0
                        ? 0m
                        : Math.Round(amt * 100m / invoiceSalesTotal, 1, MidpointRounding.AwayFromZero),
                    Count = g.Count()
                };
            })
            .OrderByDescending(p => p.AmountPaisa)
            .ToList();

        List<BiDailyPoint> dailyTrend = daily
            .Select(d => new BiDailyPoint
            {
                Date = d.Date,
                SalesPaisa = d.SalesPaisa,
                ProfitPaisa = d.ProfitPaisa
            })
            .ToList();

        // Monthly seasonality from seasonDaily
        List<BiMonthPoint> months = BuildMonthlySeasonality(seasonDaily, to);

        List<BiHeatCell> heatmap = heatRaw
            .Select(h => new BiHeatCell
            {
                Weekday = h.Weekday,
                Hour = h.Hour,
                InvoiceCount = h.Count
            })
            .ToList();

        List<BiInsight> insights = BuildInsights(categories, products, heatmap, priorCat, query.ComparePrevious);

        BiCategoryRow? bestCat = categories.OrderByDescending(c => c.MarginPercent).FirstOrDefault();
        var summary = new BiSummaryBlock
        {
            TotalSalesPaisa = sales,
            TotalProfitPaisa = profit,
            AvgMarginPercent = margin,
            BestCategoryName = bestCat?.CategoryName,
            BestCategoryMarginPercent = bestCat?.MarginPercent,
            AtRiskSkuCount = products.Count(p => p.IsDeclining),
            StarsCount = products.Count(p => p.Quadrant == BiQuadrants.Stars && !p.IsDeadStock)
        };

        return new BiDashboardResult
        {
            From = from,
            To = to,
            Kpis = kpis,
            Categories = categories,
            TopSkus = topSkus,
            CategoryProfitShare = categoryShare,
            Products = products,
            Terminals = terminals,
            Payments = payments,
            DailyTrend = dailyTrend,
            MonthlySeasonality = months,
            Heatmap = heatmap,
            Insights = insights,
            Summary = summary
        };
    }

    public async Task<string> ExportExpenseReportCsvAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseCategorySummary> rows =
            await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        StringBuilder builder = new();
        builder.AppendLine("ExpenseCategory,TotalAmountPaisa,TransactionCount");

        foreach (ExpenseCategorySummary row in rows)
        {
            builder.Append(CsvEscape(row.ExpenseCategory));
            builder.Append(',');
            builder.Append(row.TotalAmountPaisa.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(row.TransactionCount.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    public async Task<string> ExportExpenseReportJsonAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ExpenseCategorySummary> rows =
            await GetExpenseSummaryByCategoryAsync(from, to, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            from,
            to,
            generatedAt = DateTimeOffset.UtcNow,
            categories = rows
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for reporting operations.");
        }
    }

    private static IQueryable<Models.SalesItem> FilteredSalesItems(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query)
    {
        IQueryable<Models.SalesItem> q = context.SalesItems
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.Invoice.TenantId == tenantId
                && i.Invoice.CreatedAt >= from
                && i.Invoice.CreatedAt <= to);

        if (query.CategoryId is Guid categoryId)
        {
            q = q.Where(i => i.Product.CategoryId == categoryId);
        }

        if (query.TerminalId is Guid terminalId)
        {
            q = q.Where(i => i.Invoice.TerminalId == terminalId);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
        {
            string method = query.PaymentMethod.Trim();
            q = q.Where(i => i.Invoice.PaymentMethod == method);
        }

        return q;
    }

    private static IQueryable<Models.SalesInvoice> FilteredInvoices(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query)
    {
        IQueryable<Models.SalesInvoice> q = context.SalesInvoices
            .AsNoTracking()
            .Where(i =>
                i.TenantId == tenantId
                && i.CreatedAt >= from
                && i.CreatedAt <= to);

        if (query.TerminalId is Guid terminalId)
        {
            q = q.Where(i => i.TerminalId == terminalId);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentMethod))
        {
            string method = query.PaymentMethod.Trim();
            q = q.Where(i => i.PaymentMethod == method);
        }

        if (query.CategoryId is Guid categoryId)
        {
            q = q.Where(i => i.Items.Any(li => li.Product.CategoryId == categoryId));
        }

        return q;
    }

    private static async Task<List<LineAgg>> LoadLineAggregatesAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await FilteredSalesItems(context, tenantId, from, to, query)
            .Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Product.CategoryId,
                CategoryName = i.Product.Category != null ? i.Product.Category.Name : null,
                i.Quantity,
                Revenue = i.Quantity * i.UnitPricePaisa,
                Cost = i.Quantity * i.UnitCostPaisa
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.ProductId, r.ProductName, r.CategoryId, r.CategoryName })
            .Select(g => new LineAgg(
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.CategoryId,
                g.Key.CategoryName,
                g.Sum(x => x.Quantity),
                (long)Math.Round(g.Sum(x => x.Revenue), MidpointRounding.AwayFromZero),
                (long)Math.Round(g.Sum(x => x.Cost), MidpointRounding.AwayFromZero)))
            .ToList();
    }

    private static async Task<List<InvoiceAgg>> LoadInvoiceAggregatesAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query,
        CancellationToken cancellationToken)
    {
        return await FilteredInvoices(context, tenantId, from, to, query)
            .Select(i => new InvoiceAgg(i.TerminalId, i.PaymentMethod, i.TotalAmountPaisa))
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<DailyBucket>> LoadDailyBucketsAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await FilteredSalesItems(context, tenantId, from, to, query)
            .Select(i => new
            {
                i.Invoice.CreatedAt,
                Revenue = i.Quantity * i.UnitPricePaisa,
                Cost = i.Quantity * i.UnitCostPaisa
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => DateOnly.FromDateTime(r.CreatedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyBucket(
                g.Key,
                (long)Math.Round(g.Sum(x => x.Revenue), MidpointRounding.AwayFromZero),
                (long)Math.Round(g.Sum(x => x.Revenue - x.Cost), MidpointRounding.AwayFromZero)))
            .ToList();
    }

    private static async Task<List<HeatRaw>> LoadHeatmapAsync(
        WebPosDbContext context,
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        BiDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var stamps = await FilteredInvoices(context, tenantId, from, to, query)
            .Select(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return stamps
            .GroupBy(t => new
            {
                Weekday = (int)t.UtcDateTime.DayOfWeek,
                Hour = t.UtcDateTime.Hour
            })
            .Select(g => new HeatRaw(g.Key.Weekday, g.Key.Hour, g.Count()))
            .ToList();
    }

    private static List<BiMonthPoint> BuildMonthlySeasonality(List<DailyBucket> daily, DateTimeOffset to)
    {
        DateOnly endMonth = new(to.Year, to.Month, 1);
        List<BiMonthPoint> points = [];
        long? prevSales = null;

        for (int i = 5; i >= 0; i--)
        {
            DateOnly monthStart = endMonth.AddMonths(-i);
            DateOnly monthEnd = monthStart.AddMonths(1);
            long sales = daily
                .Where(d => d.Date >= monthStart && d.Date < monthEnd)
                .Sum(d => d.SalesPaisa);

            decimal? mom = prevSales is null ? null : DeltaPct(sales, prevSales.Value);
            points.Add(new BiMonthPoint
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                Label = monthStart.ToString("MMM", CultureInfo.InvariantCulture),
                SalesPaisa = sales,
                MomPercent = mom
            });
            prevSales = sales;
        }

        return points;
    }

    private static List<BiInsight> BuildInsights(
        List<BiCategoryRow> categories,
        List<BiProductRow> products,
        List<BiHeatCell> heatmap,
        Dictionary<(Guid Id, string Name), (long Rev, long Cost)> priorCat,
        bool comparePrevious)
    {
        List<BiInsight> insights = [];

        if (comparePrevious && categories.Count > 0)
        {
            BiCategoryRow? dropped = categories
                .Where(c => c.TrendPercent is < 0)
                .OrderBy(c => c.TrendPercent)
                .FirstOrDefault();
            if (dropped?.TrendPercent is decimal drop)
            {
                priorCat.TryGetValue((dropped.CategoryId, dropped.CategoryName), out var prev);
                decimal prevMargin = Pct(prev.Rev - prev.Cost, prev.Rev);
                insights.Add(new BiInsight
                {
                    Severity = "warning",
                    Title = $"{dropped.CategoryName} margin pressure",
                    Detail =
                        $"Profit trend {drop:0.0}% vs prior period (margin now {dropped.MarginPercent:0.0}% vs {prevMargin:0.0}% before)."
                });
            }
        }

        BiProductRow? declining = products
            .Where(p => p.IsDeclining)
            .OrderBy(p => p.GrowthPercent)
            .FirstOrDefault();
        if (declining is not null)
        {
            insights.Add(new BiInsight
            {
                Severity = "danger",
                Title = $"{declining.ProductName} declining {declining.GrowthPercent:0.0}%",
                Detail = "Sales down sharply vs prior period. Check stock, pricing, and nearby promos."
            });
        }

        BiCategoryRow? best = categories.OrderByDescending(c => c.MarginPercent).FirstOrDefault();
        if (best is not null && best.MarginPercent > 0)
        {
            insights.Add(new BiInsight
            {
                Severity = "success",
                Title = $"{best.CategoryName} = {best.MarginPercent:0.0}% margin",
                Detail = "Highest-margin category — consider front-shelf push and bundles."
            });
        }

        BiHeatCell? peak = heatmap.OrderByDescending(h => h.InvoiceCount).FirstOrDefault();
        if (peak is { InvoiceCount: > 0 })
        {
            string day = CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedDayNames[peak.Weekday];
            insights.Add(new BiInsight
            {
                Severity = "info",
                Title = $"Peak {day} {peak.Hour:00}:00–{peak.Hour:00}:59",
                Detail = $"{peak.InvoiceCount} invoices in that hour band — staff for the rush."
            });
        }

        return insights.Take(4).ToList();
    }

    private static void NormalizePercents(List<BiShareSlice> slices)
    {
        if (slices.Count == 0)
        {
            return;
        }

        decimal sum = slices.Sum(s => s.PercentOfTotal);
        if (sum == 0 || Math.Abs(sum - 100m) < 0.05m)
        {
            return;
        }

        // Leave as-is; UI can render relative bars from AmountPaisa.
    }

    private static List<long> SampleSparkline(List<long> series, int maxPoints)
    {
        if (series.Count == 0)
        {
            return [0, 0, 0, 0];
        }

        if (series.Count <= maxPoints)
        {
            return series;
        }

        List<long> sampled = [];
        for (int i = 0; i < maxPoints; i++)
        {
            int idx = (int)Math.Round(i * (series.Count - 1m) / (maxPoints - 1), MidpointRounding.AwayFromZero);
            sampled.Add(series[idx]);
        }

        return sampled;
    }

    private static long Median(List<long> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        List<long> sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2
            : sorted[mid];
    }

    private static decimal Pct(long numerator, long denominator) =>
        denominator == 0
            ? 0m
            : Math.Round(numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);

    private static decimal? DeltaPct(long current, long previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0m : 100m;
        }

        return Math.Round((current - previous) * 100m / previous, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal? DeltaPct(decimal current, decimal previous)
    {
        if (previous == 0)
        {
            return current == 0 ? 0m : 100m;
        }

        return Math.Round((current - previous) * 100m / previous, 1, MidpointRounding.AwayFromZero);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }

    private sealed record LineAgg(
        Guid ProductId,
        string ProductName,
        Guid? CategoryId,
        string? CategoryName,
        decimal Quantity,
        long RevenuePaisa,
        long CostPaisa);

    private sealed record InvoiceAgg(Guid? TerminalId, string PaymentMethod, long TotalAmountPaisa);

    private sealed record DailyBucket(DateOnly Date, long SalesPaisa, long ProfitPaisa);

    private sealed record HeatRaw(int Weekday, int Hour, int Count);
}
