namespace WebPos.Core.Models;

public class PurchaseReturn
{
    public Guid Id { get; set; }

    public Guid OriginalPurchaseOrderId { get; set; }

    public Guid ManagerId { get; set; }

    public Guid SupplierId { get; set; }

    public long TotalCreditDeductionPaisa { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public PurchaseOrder OriginalPurchaseOrder { get; set; } = null!;

    public User Manager { get; set; } = null!;

    public Party Supplier { get; set; } = null!;

    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}
