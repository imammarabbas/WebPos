namespace WebPos.Core.Abstractions;

public sealed class PartyLedgerEntryDto
{
    public required Guid Id { get; init; }

    public required Guid PartyId { get; init; }

    public required string Type { get; init; }

    public required string ReferenceType { get; init; }

    public required string PaymentMethod { get; init; }

    public long OldBalancePaisa { get; init; }

    public long TransactionAmountPaisa { get; init; }

    public long DebitPaisa { get; init; }

    public long CreditPaisa { get; init; }

    public long NewBalancePaisa { get; init; }

    public long RunningBalancePaisa => NewBalancePaisa;

    public string? InvoiceNo { get; init; }

    public Guid? PurchaseOrderId { get; init; }

    public required string ReferenceDetails { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PartyLedgerSlipDto
{
    public required Guid PartyId { get; init; }

    public required string PartyName { get; init; }

    public required string PartyType { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public long OpeningBalancePaisa { get; init; }

    public long ClosingBalancePaisa { get; init; }

    public long TotalDebitPaisa { get; init; }

    public long TotalCreditPaisa { get; init; }

    public required IReadOnlyList<PartyLedgerEntryDto> Entries { get; init; }
}

public sealed class OpenPartySlipDto
{
    public string? InvoiceNo { get; init; }

    public Guid? PurchaseOrderId { get; init; }

    public required string Reference { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public long TotalPaisa { get; init; }

    public long AmountPaidPaisa { get; init; }

    public long OutstandingPaisa { get; init; }
}

public sealed class PartyFinanceKpisDto
{
    public required string Role { get; init; }

    public long TotalReceivablePaisa { get; init; }

    public long Overdue30PlusPaisa { get; init; }

    public long AdvanceReceivedPaisa { get; init; }

    public long TodayCollectionPaisa { get; init; }

    public long TotalPayablePaisa { get; init; }

    public long DueThisWeekPaisa { get; init; }

    public long AdvancePaidPaisa { get; init; }

    public long TodayPaidPaisa { get; init; }
}

public sealed class PartyAgeingDto
{
    public Guid PartyId { get; init; }

    public long Bucket0To30Paisa { get; init; }

    public long Bucket31To60Paisa { get; init; }

    public long Bucket61To90Paisa { get; init; }

    public long Bucket90PlusPaisa { get; init; }
}

public sealed class RecentPartyPaymentDto
{
    public required Guid Id { get; init; }

    public required Guid PartyId { get; init; }

    public required string PartyName { get; init; }

    public required string PartyType { get; init; }

    public long AmountPaisa { get; init; }

    public required string PaymentMethod { get; init; }

    public required string ReferenceDetails { get; init; }

    public bool IsAdvance { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PartyLedgerDashboardDto
{
    public required Guid PartyId { get; init; }

    public required string PartyName { get; init; }

    public required string PartyType { get; init; }

    public long OpeningBalancePaisa { get; init; }

    public long SalesOrPurchasesPaisa { get; init; }

    public long PaidPaisa { get; init; }

    public long ReturnsPaisa { get; init; }

    public long DiscountsPaisa { get; init; }

    public long AdjustmentsPaisa { get; init; }

    public long ClosingBalancePaisa { get; init; }

    public required IReadOnlyList<PartyLedgerEntryDto> Entries { get; init; }
}

public interface IPartyLedgerService
{
    Task RecordTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a party ledger row. Must run inside an ambient transaction.
    /// Returns the new ledger entry id (for payment allocations).
    /// </summary>
    Task<Guid> RecordPartyTransactionAsync(
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

    Task<IReadOnlyList<PartyLedgerEntryDto>> ListByPartyAsync(
        Guid partyId,
        int limit = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<PartyLedgerSlipDto> GetSlipAsync(
        Guid partyId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenPartySlipDto>> ListOpenSlipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<PartyFinanceKpisDto> GetPartyFinanceKpisAsync(
        string role,
        CancellationToken cancellationToken = default);

    Task<PartyAgeingDto> GetAgeingAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecentPartyPaymentDto>> ListRecentPaymentsAsync(
        string role,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PartyLedgerDashboardDto> GetLedgerDashboardAsync(
        Guid partyId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
