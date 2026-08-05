namespace WebPos.Core.Abstractions;

public sealed class BiDashboardQuery
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? TerminalId { get; init; }

    public string? PaymentMethod { get; init; }

    public bool ComparePrevious { get; init; } = true;
}

public static class BiQuadrants
{
    public const string Stars = "Stars";
    public const string Workhorses = "Workhorses";
    public const string Opportunities = "Opportunities";
    public const string Dogs = "Dogs";
}

public sealed class BiDashboardResult
{
    public required DateTimeOffset From { get; init; }

    public required DateTimeOffset To { get; init; }

    public required BiKpis Kpis { get; init; }

    public required IReadOnlyList<BiCategoryRow> Categories { get; init; }

    public required IReadOnlyList<BiSkuRow> TopSkus { get; init; }

    public required IReadOnlyList<BiShareSlice> CategoryProfitShare { get; init; }

    public required IReadOnlyList<BiProductRow> Products { get; init; }

    public required IReadOnlyList<BiShareSlice> Terminals { get; init; }

    public required IReadOnlyList<BiShareSlice> Payments { get; init; }

    public required IReadOnlyList<BiDailyPoint> DailyTrend { get; init; }

    public required IReadOnlyList<BiMonthPoint> MonthlySeasonality { get; init; }

    public required IReadOnlyList<BiHeatCell> Heatmap { get; init; }

    public required IReadOnlyList<BiInsight> Insights { get; init; }

    public required BiSummaryBlock Summary { get; init; }
}

public sealed class BiKpis
{
    public long SalesPaisa { get; init; }

    public long GrossProfitPaisa { get; init; }

    public decimal MarginPercent { get; init; }

    public decimal ItemsSold { get; init; }

    public long AvgBasketPaisa { get; init; }

    public int Transactions { get; init; }

    public decimal? SalesDeltaPercent { get; init; }

    public decimal? GrossProfitDeltaPercent { get; init; }

    public decimal? MarginDeltaPercent { get; init; }

    public decimal? ItemsSoldDeltaPercent { get; init; }

    public decimal? AvgBasketDeltaPercent { get; init; }

    public decimal? TransactionsDeltaPercent { get; init; }

    public required IReadOnlyList<long> SalesSparklinePaisa { get; init; }

    public required IReadOnlyList<long> ProfitSparklinePaisa { get; init; }
}

public sealed class BiCategoryRow
{
    public required Guid CategoryId { get; init; }

    public required string CategoryName { get; init; }

    public long SalesPaisa { get; init; }

    public long ProfitPaisa { get; init; }

    public decimal MarginPercent { get; init; }

    public decimal PercentOfTotal { get; init; }

    public decimal? TrendPercent { get; init; }
}

public sealed class BiSkuRow
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public long TotalProfitPaisa { get; init; }

    public long PerUnitProfitPaisa { get; init; }

    public decimal Quantity { get; init; }
}

public sealed class BiShareSlice
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public long AmountPaisa { get; init; }

    public decimal PercentOfTotal { get; init; }

    public int Count { get; init; }
}

public sealed class BiProductRow
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public Guid? CategoryId { get; init; }

    public required string CategoryName { get; init; }

    public decimal Quantity { get; init; }

    public long SalesPaisa { get; init; }

    public long ProfitPaisa { get; init; }

    public decimal MarginPercent { get; init; }

    public decimal? GrowthPercent { get; init; }

    public required string Quadrant { get; init; }

    public bool IsDeclining { get; init; }

    public bool IsLowMargin { get; init; }

    public bool IsDeadStock { get; init; }
}

public sealed class BiDailyPoint
{
    public required DateOnly Date { get; init; }

    public long SalesPaisa { get; init; }

    public long ProfitPaisa { get; init; }
}

public sealed class BiMonthPoint
{
    public required int Year { get; init; }

    public required int Month { get; init; }

    public required string Label { get; init; }

    public long SalesPaisa { get; init; }

    public decimal? MomPercent { get; init; }
}

public sealed class BiHeatCell
{
    public int Weekday { get; init; }

    public int Hour { get; init; }

    public int InvoiceCount { get; init; }
}

public sealed class BiInsight
{
    public required string Severity { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }
}

public sealed class BiSummaryBlock
{
    public long TotalSalesPaisa { get; init; }

    public long TotalProfitPaisa { get; init; }

    public decimal AvgMarginPercent { get; init; }

    public string? BestCategoryName { get; init; }

    public decimal? BestCategoryMarginPercent { get; init; }

    public int AtRiskSkuCount { get; init; }

    public int StarsCount { get; init; }
}
