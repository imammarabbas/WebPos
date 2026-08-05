using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class PurchaseReturnItem : BaseEntity
{
    public Guid Id { get; set; }

    public Guid PurchaseReturnId { get; set; }

    public Guid ProductId { get; set; }

    public Guid BatchId { get; set; }

    public decimal Quantity { get; set; }

    public long CostPerUnitPaisa { get; set; }

    public PurchaseReturn PurchaseReturn { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch Batch { get; set; } = null!;
}
