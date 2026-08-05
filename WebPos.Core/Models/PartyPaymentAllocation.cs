using WebPos.Core.Entities;

namespace WebPos.Core.Models;

/// <summary>Links a party payment ledger row to one or more invoices / POs.</summary>
public class PartyPaymentAllocation : BaseEntity
{
    public Guid Id { get; set; }

    public Guid PartyLedgerId { get; set; }

    public string? InvoiceNo { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    public long AmountPaisa { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public PartyLedger PartyLedger { get; set; } = null!;

    public SalesInvoice? Invoice { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }
}
