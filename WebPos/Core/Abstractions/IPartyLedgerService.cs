namespace WebPos.Core.Abstractions;

public interface IPartyLedgerService
{
    Task RecordTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default);

    Task RecordPartyTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a zero-balance opening ledger marker for a newly created supplier.
    /// Must be called inside an ambient <see cref="ITransactionService"/> transaction.
    /// </summary>
    Task InitializeSupplierLedgerAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);
}
