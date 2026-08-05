namespace WebPos.Core.Abstractions;

public sealed class RecordExpenseRequest
{
    /// <summary>
    /// When set, must be an OPEN shift; cash payments reduce that till's expected cash.
    /// When null, overhead expense — GL only, no drawer impact.
    /// </summary>
    public Guid? ShiftId { get; init; }

    public required Guid LoggedByUserId { get; init; }

    public string? VoucherNo { get; init; }

    public required string Description { get; init; }

    public required string ExpenseCategory { get; init; }

    public required string ReceiptReference { get; init; }

    public required string PaymentMethod { get; init; }

    public Guid? CashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public bool IsRecurring { get; init; }
}

public sealed class RecordExpenseResult
{
    public required Guid ExpenseId { get; init; }

    public required string VoucherNo { get; init; }

    public Guid TransactionGroupId { get; init; }
}

public sealed class ExpenseListItemDto
{
    public required Guid Id { get; init; }

    public Guid? ShiftId { get; init; }

    public required string VoucherNo { get; init; }

    public required string Description { get; init; }

    public required string ExpenseCategory { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReceiptReference { get; init; }

    public long AmountPaisa { get; init; }

    public bool IsRecurring { get; init; }

    public Guid LoggedByUserId { get; init; }

    public DateTimeOffset LoggedAt { get; init; }
}

public interface IExpenseService
{
    Task<RecordExpenseResult> RecordShiftExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordExpenseResult> RecordRecurringExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExpenseListItemDto>> ListExpensesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
