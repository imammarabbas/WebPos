using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class PurchaseOrder : BaseEntity
{
    public Guid Id { get; set; }

    public string SupplierInvoiceNo { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid ReceiverId { get; set; }

    public long SubTotalPaisa { get; set; }

    public long DiscountPaisa { get; set; }

    public long NetPayablePaisa { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    /// <summary>Sum of supplier payment allocations against this PO.</summary>
    public long AmountPaidPaisa { get; set; }

    /// <summary>
    /// True after ReceiveStockAsync posts inventory batches and AP ledger entries.
    /// Guards against double-posting the same invoice.
    /// </summary>
    public bool IsReceived { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Party Supplier { get; set; } = null!;

    public User Receiver { get; set; } = null!;

    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();

    public ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();

    public ICollection<PurchaseReturn> PurchaseReturns { get; set; } = new List<PurchaseReturn>();
}
