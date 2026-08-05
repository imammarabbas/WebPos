namespace WebPos.Core.Abstractions;

public sealed class MasterOverviewDto
{
    public int ProductCount { get; init; }

    public int CustomerCount { get; init; }

    public int SupplierCount { get; init; }

    public int LowStockCount { get; init; }

    public long TodayRevenuePaisa { get; init; }

    public int TodayOrderCount { get; init; }

    public bool TillOpen { get; init; }

    public bool ApiOnline { get; init; } = true;

    public required IReadOnlyList<LowStockItemDto> LowStockItems { get; init; }

    public required IReadOnlyList<CategoryStockDto> InventoryByCategory { get; init; }

    public required IReadOnlyList<ActivityItemDto> RecentActivity { get; init; }
}

public sealed class LowStockItemDto
{
    public required Guid ProductId { get; init; }

    public required string Name { get; init; }

    public decimal AvailableStock { get; init; }

    public decimal MinStockQty { get; init; }
}

public sealed class CategoryStockDto
{
    public required string CategoryName { get; init; }

    public int ProductCount { get; init; }

    public int Percent { get; init; }
}

public sealed class ActivityItemDto
{
    public required string Kind { get; init; }

    public required string Title { get; init; }

    public required string Detail { get; init; }

    public DateTimeOffset At { get; init; }
}

public interface IMasterOverviewService
{
    Task<MasterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}

