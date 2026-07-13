namespace WebPos.Core.Models;

public class GeneralLedgerEntry
{
    public Guid Id { get; set; }

    public Guid TransactionGroupId { get; set; }

    public string AccountCode { get; set; } = string.Empty;

    public long DebitPaisa { get; set; }

    public long CreditPaisa { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public string ReferenceNo { get; set; } = string.Empty;

    public string ReferenceDetails { get; set; } = string.Empty;

    public Guid? ShiftId { get; set; }

    public Guid? PartyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
