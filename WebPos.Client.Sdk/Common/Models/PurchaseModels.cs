namespace Common.Models;

public sealed class ManagerPinVerifiedDto
{
    public bool Verified { get; init; }
}

public sealed class QuickReceiveNewSupplierDto
{
    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;
}

public sealed class QuickReceiveNewProductDto
{
    public required string Name { get; init; }

    public string Sku { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public string ShortCode { get; init; } = string.Empty;

    public bool IsLoose { get; init; }

    public string Brand { get; init; } = string.Empty;

    public string BaseUnit { get; init; } = "PCS";
}

public sealed class QuickReceiveLineDto
{
    public Guid? ProductId { get; init; }

    public QuickReceiveNewProductDto? NewProduct { get; init; }

    public decimal Quantity { get; init; }

    public decimal BonusQuantity { get; init; }

    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public DateOnly? ExpiryDate { get; init; }

    public string? RackLocation { get; init; }
}

public sealed class QuickReceiveRequestDto
{
    public Guid? SupplierId { get; init; }

    public QuickReceiveNewSupplierDto? NewSupplier { get; init; }

    public required Guid ReceiverId { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    public DateTimeOffset PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public string? ManagerPin { get; init; }

    public required IReadOnlyList<QuickReceiveLineDto> Lines { get; init; }
}

public sealed class ReceiveStockResultDto
{
    public required Guid PurchaseOrderId { get; init; }

    public long NetPayablePaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public IReadOnlyList<Guid> BatchIds { get; init; } = [];
}

public sealed class CreatePurchaseLineDto
{
    public required Guid ProductId { get; init; }

    public decimal Quantity { get; init; }

    public decimal BonusQuantity { get; init; }

    public long PurchasePricePaisa { get; init; }

    public long RetailPricePaisa { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public DateOnly? ExpiryDate { get; init; }

    public string? RackLocation { get; init; }
}

public sealed class CreatePurchaseRequestDto
{
    public required Guid SupplierId { get; init; }

    public required Guid ReceiverId { get; init; }

    public required string SupplierInvoiceNo { get; init; }

    public DateTimeOffset PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public required IReadOnlyList<CreatePurchaseLineDto> Lines { get; init; }
}

public sealed class UpdateOpenPurchaseRequestDto
{
    public required string SupplierInvoiceNo { get; init; }

    public Guid? SupplierId { get; init; }

    public DateTimeOffset? PurchaseDate { get; init; }

    public long DiscountPaisa { get; init; }

    public required IReadOnlyList<CreatePurchaseLineDto> Lines { get; init; }
}

public sealed class CreatePurchaseOrderResultDto
{
    public required Guid PurchaseOrderId { get; init; }

    public long SubTotalPaisa { get; init; }

    public long DiscountPaisa { get; init; }

    public long NetPayablePaisa { get; init; }

    public bool IsReceived { get; init; }
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

public sealed class StoreStatusDto
{
    public bool TillOpen { get; init; }

    public bool ApiOnline { get; init; }

    public int OpenShiftCount { get; init; }

    public string StoreName { get; init; } = string.Empty;

    public string PosDisplayName { get; init; } = string.Empty;
}
