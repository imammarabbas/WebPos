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

public sealed class PurchaseOrderSummaryDto
{
    public required Guid Id { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    public required Guid SupplierId { get; init; }

    public string? SupplierName { get; init; }

    public long NetPayablePaisa { get; init; }

    public bool IsReceived { get; init; }

    public required string PaymentStatus { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public int LineCount { get; init; }
}

public sealed class PurchaseOrderDetailDto
{
    public required Guid Id { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    public required Guid SupplierId { get; init; }

    public string? SupplierName { get; init; }

    public required Guid ReceiverId { get; init; }

    public long SubTotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long NetPayablePaisa { get; init; }

    public bool IsReceived { get; init; }

    public required string PaymentStatus { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required IReadOnlyList<PurchaseOrderLineDto> Lines { get; init; }
}

public sealed class PurchaseOrderLineDto
{
    public required Guid Id { get; init; }

    public required Guid ProductId { get; init; }

    public string? ProductName { get; init; }

    public decimal Quantity { get; init; }

    public decimal BonusQuantity { get; init; }

    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public required string BatchNumber { get; init; }

    public DateOnly? ExpiryDate { get; init; }

    public string? RackLocation { get; init; }
}

public sealed class UpdateOpenPurchaseRequest
{
    public required string SupplierInvoiceNo { get; init; }

    public Guid? SupplierId { get; init; }

    public DateTimeOffset? PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public required IReadOnlyList<CreatePurchaseLineRequest> Lines { get; init; }
}

public sealed class QuickReceiveNewSupplierRequest
{
    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;
}

public sealed class QuickReceiveNewProductRequest
{
    public required string Name { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public string ShortCode { get; init; } = string.Empty;

    public bool IsLoose { get; init; }

    public string Brand { get; init; } = string.Empty;

    public string BaseUnit { get; init; } = "PCS";
}

public sealed class QuickReceiveLineRequest
{
    public Guid? ProductId { get; init; }

    public QuickReceiveNewProductRequest? NewProduct { get; init; }

    public decimal Quantity { get; init; }

    public decimal BonusQuantity { get; init; }

    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public DateOnly? ExpiryDate { get; init; }

    public string? RackLocation { get; init; }
}

public sealed class QuickReceiveRequest
{
    public Guid? SupplierId { get; init; }

    public QuickReceiveNewSupplierRequest? NewSupplier { get; init; }

    public required Guid ReceiverId { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    public DateTimeOffset PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public string? ManagerPin { get; init; }

    public required IReadOnlyList<QuickReceiveLineRequest> Lines { get; init; }
}

public interface IPurchaseService
{
    Task<CreatePurchaseOrderResult> CreatePurchaseOrderAsync(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResult> ReceiveStockAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResult> QuickReceiveAsync(
        QuickReceiveRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDetailDto?> GetAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDetailDto> UpdateOpenPurchaseAsync(
        Guid purchaseOrderId,
        UpdateOpenPurchaseRequest request,
        CancellationToken cancellationToken = default);
}
