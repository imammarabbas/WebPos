using WebPos.Core.Entities;

namespace WebPos.Core.Models;

/// <summary>
/// Auditable till float adjustment (Cash In / Cash Out) on an OPEN shift.
/// Align-only Cash In raises ExpectedCash without a second till GL debit.
/// </summary>
public class ShiftCashMovement : BaseEntity
{
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public Guid TillCashAccountId { get; set; }

    /// <summary>IN or OUT.</summary>
    public string Direction { get; set; } = string.Empty;

    public long AmountPaisa { get; set; }

    /// <summary>Portion of Cash In that only raised ExpectedCash (already on till GL).</summary>
    public long AlignPaisa { get; set; }

    /// <summary>Portion that posted Dr till / Cr LIABILITY:UNREGISTERED_CASH.</summary>
    public long ExcessPaisa { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    /// <summary>UNRECONCILED, RECONCILED, or REVERSED.</summary>
    public string Status { get; set; } = string.Empty;

    public string? LinkedReferenceType { get; set; }

    public Guid? LinkedReferenceId { get; set; }

    public Guid? TransactionGroupId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public CashierShift? Shift { get; set; }

    public CashAccount? TillCashAccount { get; set; }
}
