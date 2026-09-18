namespace WebPos.Core.Abstractions;

public sealed class CashTransferResult
{
    public required Guid TransactionGroupId { get; init; }

    public required string ReferenceNo { get; init; }

    public required Guid FromAccountId { get; init; }

    public required Guid ToAccountId { get; init; }

    public long AmountPaisa { get; init; }
}

public sealed class CashTransferDto
{
    public required Guid TransactionGroupId { get; init; }

    public required string ReferenceNo { get; init; }

    public required Guid FromAccountId { get; init; }

    public required string FromAccountName { get; init; }

    public required string FromAccountCode { get; init; }

    public required Guid ToAccountId { get; init; }

    public required string ToAccountName { get; init; }

    public required string ToAccountCode { get; init; }

    public long AmountPaisa { get; init; }

    public required string Notes { get; init; }

    public Guid? ShiftId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public interface ICashTransferService
{
    Task<CashTransferResult> TransferAsync(
        Guid fromAccountId,
        Guid toAccountId,
        long amountPaisa,
        string? notes,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes off till GL that exceeds physical drawer ExpectedCash (cash shortage expense).
    /// </summary>
    Task<TillShortageResult> RecordTillShortageAsync(
        RecordTillShortageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes physical cash in the till: raises ExpectedCash (Available).
    /// Debits till GL only for the excess beyond the current ledger-over-drawer gap.
    /// </summary>
    Task<TillCashMovementResult> RecordTillCashInAsync(
        RecordTillCashInRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physical cash leaving the till without a business document.
    /// </summary>
    Task<TillCashMovementResult> RecordTillCashOutAsync(
        RecordTillCashOutRequest request,
        CancellationToken cancellationToken = default);

    Task<TillCashMovementResult> ReconcileTillCashMovementAsync(
        ReconcileTillCashMovementRequest request,
        CancellationToken cancellationToken = default);

    Task<TillCashMovementResult> ReverseTillCashMovementAsync(
        Guid movementId,
        Guid? reversedByUserId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TillCashMovementDto>> ListTillCashMovementsAsync(
        Guid? shiftId = null,
        Guid? tillCashAccountId = null,
        bool unreconciledOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(
        Guid? accountId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>Owner capital into an existing cash account (not sale revenue).</summary>
    Task<CapitalFundingResult> RecordOwnerInvestmentAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Loan cash into an existing cash account (not income revenue).</summary>
    Task<CapitalFundingResult> RecordLoanReceivedAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Repay loan from an existing cash account (not a normal expense).</summary>
    Task<CapitalFundingResult> RecordLoanRepaymentAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CapitalFundingRequest
{
    public required Guid CashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public string? Note { get; init; }

    /// <summary>Required when CashAccountId is a Till (open shift).</summary>
    public Guid? ShiftId { get; init; }

    public Guid? CreatedByUserId { get; init; }
}

public sealed class CapitalFundingResult
{
    public required Guid TransactionGroupId { get; init; }

    public required string ReferenceNo { get; init; }

    public required string TransactionType { get; init; }

    public required Guid CashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public long AccountGlBalanceAfterPaisa { get; init; }

    public long? ExpectedCashAfterPaisa { get; init; }
}

public sealed class RecordTillShortageRequest
{
    public required Guid ShiftId { get; init; }

    public required Guid TillCashAccountId { get; init; }

    public long ShortagePaisa { get; init; }

    public Guid? LoggedByUserId { get; init; }

    public string? Notes { get; init; }
}

public sealed class TillShortageResult
{
    public required Guid TransactionGroupId { get; init; }

    public required string ReferenceNo { get; init; }

    public long ShortagePaisa { get; init; }

    public long TillGlBalanceAfterPaisa { get; init; }

    public Guid? MovementId { get; init; }
}

public sealed class RecordTillCashInRequest
{
    public required Guid ShiftId { get; init; }

    public required Guid TillCashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public required string Reason { get; init; }

    public string? Note { get; init; }

    public Guid? CreatedByUserId { get; init; }
}

public sealed class RecordTillCashOutRequest
{
    public required Guid ShiftId { get; init; }

    public required Guid TillCashAccountId { get; init; }

    public long AmountPaisa { get; init; }

    public required string Reason { get; init; }

    public string? Note { get; init; }

    public Guid? CreatedByUserId { get; init; }
}

public sealed class ReconcileTillCashMovementRequest
{
    public required Guid MovementId { get; init; }

    public required string LinkedReferenceType { get; init; }

    public required Guid LinkedReferenceId { get; init; }

    public Guid? ReconciledByUserId { get; init; }

    public string? Notes { get; init; }
}

public sealed class TillCashMovementResult
{
    public required Guid MovementId { get; init; }

    public required string Direction { get; init; }

    public long AmountPaisa { get; init; }

    public long AlignPaisa { get; init; }

    public long ExcessPaisa { get; init; }

    public required string Status { get; init; }

    public long ExpectedCashAfterPaisa { get; init; }

    public long TillGlBalanceAfterPaisa { get; init; }

    public Guid? TransactionGroupId { get; init; }
}

public sealed class TillCashMovementDto
{
    public required Guid Id { get; init; }

    public required Guid ShiftId { get; init; }

    public required Guid TillCashAccountId { get; init; }

    public required string Direction { get; init; }

    public long AmountPaisa { get; init; }

    public long AlignPaisa { get; init; }

    public long ExcessPaisa { get; init; }

    public required string Reason { get; init; }

    public required string Note { get; init; }

    public required string Status { get; init; }

    public string? LinkedReferenceType { get; init; }

    public Guid? LinkedReferenceId { get; init; }

    public Guid? TransactionGroupId { get; init; }

    public Guid CreatedByUserId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
