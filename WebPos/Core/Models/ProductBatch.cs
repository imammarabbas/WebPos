namespace WebPos.Core.Models;

public class ProductBatch
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public DateOnly ExpiryDate { get; set; }

    public long CostPricePaisa { get; set; }

    public long RetailPricePaisa { get; set; }

    public decimal InitialQty { get; set; }

    public decimal CurrentQty { get; set; }

    public Guid SupplierId { get; set; }

    public string? RackLocation { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Product Product { get; set; } = null!;

    public Party Supplier { get; set; } = null!;

    public PurchaseOrder? PurchaseOrder { get; set; }

    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();

    public ICollection<ProductionYieldItem> ProductionYieldItems { get; set; } = new List<ProductionYieldItem>();

    public ICollection<SalesItem> SalesItems { get; set; } = new List<SalesItem>();

    public ICollection<SalesReturnItem> SalesReturnItems { get; set; } = new List<SalesReturnItem>();

    public ICollection<PurchaseReturnItem> PurchaseReturnItems { get; set; } = new List<PurchaseReturnItem>();

    public ICollection<DamagedStockLog> DamagedStockLogs { get; set; } = new List<DamagedStockLog>();
}
