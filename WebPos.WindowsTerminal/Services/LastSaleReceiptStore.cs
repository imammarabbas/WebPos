using Common.Models;

namespace WebPos.WindowsTerminal.Services;

public sealed class LastSaleReceipt
{
    public required CompleteSaleResult Sale { get; init; }

    public required IReadOnlyList<CartLine> Lines { get; init; }

    public required string PaymentLabel { get; init; }

    public string? OnlineTxnRef { get; init; }

    public long? ChangePaisa { get; init; }

    public string? CustomerPhone { get; init; }
}

public sealed class LastSaleReceiptStore
{
    public LastSaleReceipt? Current { get; private set; }

    public void Set(LastSaleReceipt receipt)
    {
        Current = receipt ?? throw new ArgumentNullException(nameof(receipt));
    }

    public void Clear() => Current = null;
}
