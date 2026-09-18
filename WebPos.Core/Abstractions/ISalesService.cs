namespace WebPos.Core.Abstractions;

public sealed class SaleLineRequest
{
    public required Guid ProductId { get; init; }

    /// <summary>Legacy; ignored for product-pool sales.</summary>
    public Guid? BatchId { get; init; }

    public string BatchNumber { get; init; } = string.Empty;

    public required string ProductName { get; init; }

    public decimal Quantity { get; init; }

    public long UnitPricePaisa { get; init; }

    public long DiscountAppliedPaisa { get; init; }
}

public sealed class CompleteSaleRequest
{
    public required string InvoiceNo { get; init; }

    public required Guid ShiftId { get; init; }

    public Guid? TerminalId { get; init; }

    public required Guid CashierId { get; init; }

    public Guid? CustomerId { get; init; }

    public required string PaymentMethod { get; init; }

    public long DiscountAmountPaisa { get; init; }

    public string? DiscountReason { get; init; }

    public required IReadOnlyList<SaleLineRequest> Lines { get; init; }
}

public sealed class CompleteSaleResult
{
    public required string InvoiceNo { get; init; }

    public required string ReceiptNumber { get; init; }

    public long TotalAmountPaisa { get; init; }

    public long GrossAmountPaisa { get; init; }

    public long DiscountAmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }
}

public interface ISalesService
{
    Task<CompleteSaleResult> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default);
}
