namespace WebPos.Core.Models;

public class PurchaseItem
{
    public Guid Id { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid ProductId { get; set; }

    public decimal QuantityReceived { get; set; }

    public decimal BonusQuantity { get; set; }

    public long CostPricePerUnitPaisa { get; set; }

    public long RetailPricePerUnitPaisa { get; set; }

    public Guid? BatchId { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch? Batch { get; set; }
}
