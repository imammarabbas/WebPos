namespace Common.Models;

public sealed class SaleLineRequest
{
    public required Guid ProductId { get; init; }

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

    /// <summary>
    /// Cash/online tendered. Null keeps prior behavior (net for non-CREDIT, 0 for CREDIT).
    /// </summary>
    public long? AmountPaidPaisa { get; init; }

    /// <summary>
    /// Selected wallet/bank account for ONLINE/BANK settlement.
    /// Ignored for CASH (till) and CREDIT (AR).
    /// </summary>
    public Guid? CashAccountId { get; init; }

    /// <summary>
    /// When tender exceeds net and a customer is selected, post excess as customer PAYMENT.
    /// </summary>
    public bool ApplyExcessAsCustomerCredit { get; init; }

    /// <summary>Online transaction reference; included in GL reference details.</summary>
    public string? OnlineTxnRef { get; init; }

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

    public long AmountPaidPaisa { get; init; }

    public long ChangePaisa { get; init; }

    public long CustomerCreditAppliedPaisa { get; init; }

    public long? CustomerBalancePaisa { get; init; }
}

public sealed class PaymentAccountDto
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>BANK, MOBILE, PETTY, OWNER, OTHER (never TILL).</summary>
    public required string Type { get; init; }

    public required string AccountCode { get; init; }

    public string? PaymentMethodKey { get; init; }
}
