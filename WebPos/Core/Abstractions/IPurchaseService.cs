namespace WebPos.Core.Abstractions;

public sealed class CreatePurchaseLineRequest
{
    public required Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    public decimal BonusQuantity { get; init; }

    /// <summary>Unit purchase price in paisa (captured for stock valuation).</summary>
    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public DateOnly? ExpiryDate { get; init; }

    public string? RackLocation { get; init; }
}

public sealed class CreatePurchaseRequest
{
    public required Guid SupplierId { get; init; }

    public required Guid ReceiverId { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    /// <summary>Purchase / invoice date. Must not be in the future.</summary>
    public DateTimeOffset PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public required IReadOnlyList<CreatePurchaseLineRequest> Lines { get; init; }
}

public sealed class CreatePurchaseOrderResult
{
    public required Guid PurchaseOrderId { get; init; }

    public long SubTotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long NetPayablePaisa { get; init; }

    public bool IsReceived { get; init; }
}

public sealed class ReceiveStockResult
{
    public required Guid PurchaseOrderId { get; init; }

    public long NetPayablePaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public IReadOnlyList<Guid> BatchIds { get; init; } = [];
}

public interface IPurchaseService
{
    Task<CreatePurchaseOrderResult> CreatePurchaseOrderAsync(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResult> ReceiveStockAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);
}
