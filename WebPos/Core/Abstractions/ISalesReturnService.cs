namespace WebPos.Core.Abstractions;

public sealed class ReturnLineRequest
{
    public required Guid ProductId { get; init; }

    public required Guid BatchId { get; init; }

    public decimal Quantity { get; init; }

    public string ReturnCondition { get; init; } = "GOOD";
}

public sealed class ReturnItemsRequest
{
    /// <summary>Original sales invoice number (invoice id).</summary>
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

public interface ISalesReturnService
{
    Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default);
}
