namespace WebPos.Core.Models;

public class SalesItem
{
    public Guid Id { get; set; }

    public string InvoiceNo { get; set; } = string.Empty;

    public Guid ProductId { get; set; }

    public Guid BatchId { get; set; }

    public decimal Quantity { get; set; }

    public long UnitPricePaisa { get; set; }

    public long DiscountAppliedPaisa { get; set; }

    public SalesInvoice Invoice { get; set; } = null!;

    public Product Product { get; set; } = null!;

    public ProductBatch Batch { get; set; } = null!;
}
