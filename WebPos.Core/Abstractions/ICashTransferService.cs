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

    Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(
        Guid? accountId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
