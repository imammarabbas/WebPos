namespace WebPos.Core.Abstractions;

/// <summary>
/// Thrown when an outflow would drive a liquidity GL balance negative.
/// </summary>
public sealed class InsufficientCashBalanceException : Exception
{
    public const string StandardMessage =
        "Operation denied: Insufficient funds. Account balance cannot drop below zero.";

    public InsufficientCashBalanceException(
        string accountCode,
        long availablePaisa,
        long requestedPaisa)
        : base(StandardMessage)
    {
        AccountCode = accountCode;
        AvailablePaisa = availablePaisa;
        RequestedPaisa = requestedPaisa;
    }

    public string AccountCode { get; }

    public long AvailablePaisa { get; }

    public long RequestedPaisa { get; }
}

/// <summary>
/// Thrown when a till outflow exceeds physical drawer ExpectedCash while till GL still shows more —
/// ghost-ledger spending is blocked until cash shortage or cash drop reconciles the gap.
/// </summary>
public sealed class TillDiscrepancyException : Exception
{
    public TillDiscrepancyException(
        Guid shiftId,
        string accountCode,
        long ledgerPaisa,
        long drawerPaisa,
        long requestedPaisa)
        : base(
            $"Till ledger exceeds drawer on {accountCode}. " +
            $"Ledger {FormatMoney.RsAndPaisa(ledgerPaisa)}, " +
            $"drawer {FormatMoney.RsAndPaisa(drawerPaisa)}, " +
            $"requested {FormatMoney.RsAndPaisa(requestedPaisa)}. " +
            "Record a cash shortage expense for the gap or cash-drop only what is in the drawer.")
    {
        ShiftId = shiftId;
        AccountCode = accountCode;
        LedgerPaisa = ledgerPaisa;
        DrawerPaisa = drawerPaisa;
        RequestedPaisa = requestedPaisa;
        GapPaisa = Math.Max(0L, ledgerPaisa - drawerPaisa);
    }

    public Guid ShiftId { get; }

    public string AccountCode { get; }

    public long LedgerPaisa { get; }

    public long DrawerPaisa { get; }

    public long RequestedPaisa { get; }

    public long GapPaisa { get; }
}

public static class FormatMoney
{
    public static string RsAndPaisa(long paisa) =>
        $"Rs {paisa / 100m:N2} ({paisa} paisa)";
}

/// <summary>Spendable liquidity rules for cash outflows.</summary>
public static class CashSpendable
{
    public static long ForNonTill(long glBalancePaisa) =>
        Math.Max(0L, glBalancePaisa);

    public static long ForTill(long glBalancePaisa, long expectedCashPaisa) =>
        Math.Max(0L, Math.Min(glBalancePaisa, expectedCashPaisa));

    /// <summary>
    /// Validates an outflow against Available/spendable cash.
    /// Ledger-vs-drawer mismatch is a UI warning (Cash In / Shortage), not a hard freeze —
    /// overspend beyond Available throws <see cref="InsufficientCashBalanceException"/>.
    /// </summary>
    public static void EnsureCanSpend(
        string accountCode,
        long spendablePaisa,
        long requestedPaisa,
        Guid? tillShiftId = null,
        long? tillLedgerPaisa = null,
        long? tillDrawerPaisa = null)
    {
        if (requestedPaisa <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero Paisa.", nameof(requestedPaisa));
        }

        if (requestedPaisa <= spendablePaisa)
        {
            return;
        }

        // tillShiftId / ledger / drawer retained for call-site compatibility; mismatch no longer locks.
        _ = tillShiftId;
        _ = tillLedgerPaisa;
        _ = tillDrawerPaisa;

        throw new InsufficientCashBalanceException(accountCode, spendablePaisa, requestedPaisa);
    }
}
