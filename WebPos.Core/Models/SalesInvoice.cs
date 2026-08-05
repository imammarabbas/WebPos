using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class SalesInvoice : BaseEntity
{
    public string InvoiceNo { get; set; } = string.Empty;

    public Guid ShiftId { get; set; }

    public Guid? TerminalId { get; set; }

    public Guid CashierId { get; set; }

    public Guid? CustomerId { get; set; }

    public long TotalAmountPaisa { get; set; }

    public long TaxAmountPaisa { get; set; }

    public long DiscountAmountPaisa { get; set; }

    public string? DiscountReason { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>Sum of settlements applied against this credit invoice.</summary>
    public long AmountPaidPaisa { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CashierShift Shift { get; set; } = null!;

    public Terminal? Terminal { get; set; }

    public User Cashier { get; set; } = null!;

    public Party? Customer { get; set; }

    public ICollection<SalesItem> Items { get; set; } = new List<SalesItem>();

    public ICollection<SalesReturn> SalesReturns { get; set; } = new List<SalesReturn>();
}
