namespace WebPos.Core.Abstractions;

public sealed class PurchaseReturnLineRequest
{
    public required Guid ProductId { get; init; }

    public required Guid BatchId { get; init; }

    public decimal Quantity { get; init; }
}

public sealed class CreatePurchaseReturnRequest
{
    public required Guid OriginalPurchaseOrderId { get; init; }

    public required Guid ManagerId { get; init; }

    public required IReadOnlyList<PurchaseReturnLineRequest> Lines { get; init; }
}

public sealed class CreatePurchaseReturnResult
{
    public required Guid PurchaseReturnId { get; init; }

    public long TotalCreditDeductionPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }
}

public interface IPurchaseReturnService
{
    Task<CreatePurchaseReturnResult> CreatePurchaseReturnAsync(
        CreatePurchaseReturnRequest request,
        CancellationToken cancellationToken = default);
}
