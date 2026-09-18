namespace Common.Models;

public sealed class PartyDto
{
    public Guid Id { get; init; }

    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }

    public long CurrentBalancePaisa { get; init; }
}

public sealed class CreatePartyRequest
{
    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}

public sealed class UpdatePartyRequest
{
    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

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
