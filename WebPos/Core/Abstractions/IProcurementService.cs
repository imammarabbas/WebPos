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

public sealed class RecordSupplierPaymentRequest
{
    public required Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceNo { get; init; }

    public Guid? ShiftId { get; init; }
}

public sealed class RecordSupplierPaymentResult
{
    public required Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }
}

public interface IProcurementService
{
    Task<RecordMilkCollectionResult> RecordMilkCollectionAsync(
        RecordMilkCollectionRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordSupplierPaymentResult> RecordSupplierPaymentAsync(
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default);
}
