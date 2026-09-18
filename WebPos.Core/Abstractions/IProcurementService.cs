namespace WebPos.Core.Abstractions;

public sealed class RecordMilkCollectionRequest
{
    public required Guid SupplierId { get; init; }

    public required string MilkType { get; init; }

    public decimal LitersReceived { get; init; }

    public decimal? FatPercent { get; init; }

    public decimal? SnfPercent { get; init; }

    public long RatePerLiterPaisa { get; init; }

    public DateTimeOffset? CollectionTime { get; init; }
}

public sealed class RecordMilkCollectionResult
{
    public required Guid CollectionId { get; init; }

    public long TotalCreditPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }
}

public sealed class PaymentAllocationRequest
{
    public string? InvoiceNo { get; init; }

    public Guid? PurchaseOrderId { get; init; }

    public long AmountPaisa { get; init; }
}

public sealed class RecordSupplierPaymentRequest
{
    public required Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceNo { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? CashAccountId { get; init; }

    /// <summary>
    /// Optional GL/cash account code (e.g. OWNER_CASH, BANK, PETTY_CASH).
    /// When set, funds the payment from that asset without requiring an open till.
    /// </summary>
    public string? AccountCode { get; init; }

    public IReadOnlyList<PaymentAllocationRequest>? Allocations { get; init; }
}

public sealed class RecordSupplierPaymentResult
{
    public required Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public Guid PartyLedgerId { get; init; }
}

public sealed class RecordCustomerPaymentRequest
{
    public required Guid CustomerId { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceNo { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? CashAccountId { get; init; }

    /// <summary>Optional receipt account code (e.g. OWNER_CASH, BANK).</summary>
    public string? AccountCode { get; init; }

    public IReadOnlyList<PaymentAllocationRequest>? Allocations { get; init; }
}

public sealed class RecordCustomerPaymentResult
{
    public required Guid CustomerId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public Guid PartyLedgerId { get; init; }
}

public sealed class RecordPartyAdjustmentRequest
{
    public required Guid PartyId { get; init; }

    public long AmountPaisa { get; init; }

    /// <summary>
    /// When true, increases the party's due balance (AR/AP).
    /// When false, reduces due (write-off / settlement adjustment).
    /// </summary>
    public bool IncreaseBalance { get; init; }

    public required string Notes { get; init; }
}

public sealed class RecordPartyAdjustmentResult
{
    public required Guid PartyId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public Guid PartyLedgerId { get; init; }
}

public interface IProcurementService
{
    Task<RecordMilkCollectionResult> RecordMilkCollectionAsync(
        RecordMilkCollectionRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordSupplierPaymentResult> RecordSupplierPaymentAsync(
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordCustomerPaymentResult> RecordCustomerPaymentAsync(
        RecordCustomerPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordPartyAdjustmentResult> RecordPartyAdjustmentAsync(
        RecordPartyAdjustmentRequest request,
        CancellationToken cancellationToken = default);
}
