namespace WebPos.Core.Abstractions;

public sealed class StockPositionSummaryDto
{
    public long ExpectedValuePaisa { get; init; }

    public long? PhysicalValuePaisa { get; init; }

    public long DifferenceValuePaisa =>
        PhysicalValuePaisa is long physical
            ? ExpectedValuePaisa - physical
            : 0;

    public long MissingUnexplainedValuePaisa =>
        Math.Max(0L, DifferenceValuePaisa);

    public required IReadOnlyList<StockProductValueDto> ByProduct { get; init; }

    public required IReadOnlyList<StockCategoryValueDto> ByCategory { get; init; }

    public DateTimeOffset? LastPhysicalCountAt { get; init; }
}

public sealed class StockProductValueDto
{
    public required Guid ProductId { get; init; }

    public required string ProductName { get; init; }

    public string? CategoryName { get; init; }

    public decimal ExpectedQty { get; init; }

    public long ExpectedValuePaisa { get; init; }

    public decimal? PhysicalQty { get; init; }

    public long? PhysicalValuePaisa { get; init; }
}

public sealed class StockCategoryValueDto
{
    public required string CategoryName { get; init; }

    public long ExpectedValuePaisa { get; init; }

    public long? PhysicalValuePaisa { get; init; }
}

public sealed class RecordStockCountRequest
{
    public DateTimeOffset? CountedAt { get; init; }

    public string? Note { get; init; }

    public Guid? CountedByUserId { get; init; }

    public required IReadOnlyList<RecordStockCountLineRequest> Lines { get; init; }
}

public sealed class RecordStockCountLineRequest
{
    public required Guid ProductId { get; init; }

    public Guid? BatchId { get; init; }

    public decimal CountedQty { get; init; }
}

public interface IStockPositionService
{
    Task<StockPositionSummaryDto> GetPositionAsync(
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<Guid> RecordPhysicalCountAsync(
        RecordStockCountRequest request,
        CancellationToken cancellationToken = default);
}
