namespace Common.Models;

public sealed class PartyDto
{
    public Guid Id { get; init; }

    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }

    public long CurrentBalancePaisa { get; init; }
}

public sealed class CreatePartyRequest
{
    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}

public sealed class UpdatePartyRequest
{
    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}

public sealed class PartyLedgerEntryDto
{
    public Guid Id { get; init; }

    public Guid PartyId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string ReferenceType { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = string.Empty;

    public long OldBalancePaisa { get; init; }

    public long TransactionAmountPaisa { get; init; }

    public long DebitPaisa { get; init; }

    public long CreditPaisa { get; init; }

    public long NewBalancePaisa { get; init; }

    public string? InvoiceNo { get; init; }

    public Guid? PurchaseOrderId { get; init; }

    public string ReferenceDetails { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class RecordCustomerPaymentRequest
{
    public required Guid CustomerId { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceNo { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? CashAccountId { get; init; }

    public string? AccountCode { get; init; }
}

public sealed class RecordCustomerPaymentResult
{
    public Guid CustomerId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public Guid PartyLedgerId { get; init; }
}

public sealed class RecordSupplierPaymentRequest
{
    public required Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceNo { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? CashAccountId { get; init; }

    public string? AccountCode { get; init; }
}

public sealed class RecordSupplierPaymentResult
{
    public Guid SupplierId { get; init; }

    public long AmountPaisa { get; init; }

    public Guid TransactionGroupId { get; init; }

    public Guid PartyLedgerId { get; init; }
}
