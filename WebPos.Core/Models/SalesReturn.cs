using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class SalesReturn : BaseEntity
{
    public Guid Id { get; set; }

    public string OriginalInvoiceNo { get; set; } = string.Empty;

    public Guid CashierId { get; set; }

    public Guid? CustomerId { get; set; }

    public long TotalRefundPaisa { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public SalesInvoice OriginalInvoice { get; set; } = null!;

    public User Cashier { get; set; } = null!;

    public Party? Customer { get; set; }

    public ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}
