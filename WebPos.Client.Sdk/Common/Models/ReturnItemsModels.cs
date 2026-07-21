namespace Common.Models;

public sealed class ReturnLineRequest
{
    public required Guid ProductId { get; init; }

    public required Guid BatchId { get; init; }

    public decimal Quantity { get; init; }

    public string ReturnCondition { get; init; } = "GOOD";
}

public sealed class ReturnItemsRequest
{
    public required string OriginalInvoiceNo { get; init; }

    public required Guid CashierId { get; init; }

    public required IReadOnlyList<ReturnLineRequest> Items { get; init; }
}

public sealed class ReturnItemsResult
{
    public required Guid SalesReturnId { get; init; }

    public required string OriginalInvoiceNo { get; init; }

    public long TotalRefundPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }
}
