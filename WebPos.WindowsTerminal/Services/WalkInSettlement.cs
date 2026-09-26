namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Maps UI tender to CompleteSale AmountPaidPaisa.
/// Walk-In + zero tender → null so SalesService uses amountPaid = net (fully paid).
/// </summary>
public static class WalkInSettlement
{
    /// <summary>
    /// Resolves the AmountPaidPaisa value for CompleteCartAsync / CompleteSale.
    /// </summary>
    public static long? ResolveAmountPaidForApi(
        bool isWalkIn,
        bool isCreditMode,
        long uiAmountPaidPaisa)
    {
        if (isCreditMode)
        {
            return null;
        }

        if (isWalkIn && uiAmountPaidPaisa == 0L)
        {
            return null;
        }

        return uiAmountPaidPaisa;
    }
}
