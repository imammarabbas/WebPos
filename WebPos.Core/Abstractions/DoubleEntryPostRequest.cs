namespace WebPos.Core.Abstractions;

public sealed class DoubleEntryPostRequest
{
    public required string TransactionType { get; init; }

    public required string ReferenceNo { get; init; }

    public string ReferenceDetails { get; init; } = string.Empty;

    public Guid? ShiftId { get; init; }

    public Guid? PartyId { get; init; }

    /// <summary>
    /// When set (e.g. sales return reversals), posts into this group instead of creating a new one.
    /// </summary>
    public Guid? TransactionGroupId { get; init; }

    public required IReadOnlyList<LedgerPosting> Postings { get; init; }
}
