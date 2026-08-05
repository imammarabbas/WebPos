namespace WebPos.Core.Abstractions;

public sealed class LedgerPosting
{
    public required string AccountCode { get; init; }

    public long DebitPaisa { get; init; }

    public long CreditPaisa { get; init; }
}
