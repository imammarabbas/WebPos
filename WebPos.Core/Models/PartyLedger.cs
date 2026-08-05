using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class PartyLedger : BaseEntity
{
    public Guid Id { get; set; }

    public Guid PartyId { get; set; }

    public string? InvoiceNo { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public long OldBalancePaisa { get; set; }

    public long TransactionAmountPaisa { get; set; }

    /// <summary>Increases party balance (AR/AP up).</summary>
    public long DebitPaisa { get; set; }

    /// <summary>Decreases party balance (payment / return).</summary>
    public long CreditPaisa { get; set; }

    public long NewBalancePaisa { get; set; }

    public string ReferenceDetails { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Party Party { get; set; } = null!;

    public ICollection<PartyPaymentAllocation> Allocations { get; set; } = new List<PartyPaymentAllocation>();
}
