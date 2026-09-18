using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class SalesItem : BaseEntity
{
    public Guid Id { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    /// <summary>Legacy batch link; null for product-pool sales.</summary>
    public Guid? BatchId { get; set; }

    public decimal Quantity { get; set; }

    public long UnitPricePaisa { get; set; }

    /// <summary>Unit cost snapshotted at sale time for historical profitability.</summary>
    public long UnitCostPaisa { get; set; }

    public long DiscountAppliedPaisa { get; set; }

    /// <summary>
    /// Parent base units deducted when this line sold an alias
    /// (<c>Quantity * DeductionMultiplier</c>). 0 for non-alias sales.
    /// </summary>
    public decimal ParentQtyDeducted { get; set; }

    public SalesInvoice Invoice { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch? Batch { get; set; }
}
