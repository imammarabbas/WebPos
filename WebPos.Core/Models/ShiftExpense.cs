using WebPos.Core.Entities;

namespace WebPos.Core.Models;

public class ShiftExpense : BaseEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// When set, expense is tied to an open till (cash can reduce ExpectedCash).
    /// When null, overhead/non-till expense — GL only, no drawer impact.
    /// </summary>
    public Guid? ShiftId { get; set; }

    public string VoucherNo { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public long AmountPaisa { get; set; }

    public string ExpenseCategory { get; set; } = string.Empty;

    public string ReceiptReference { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public bool IsRecurring { get; set; }

    public Guid LoggedByUserId { get; set; }

    public DateTimeOffset LoggedAt { get; set; }

    public CashierShift? Shift { get; set; }
}
