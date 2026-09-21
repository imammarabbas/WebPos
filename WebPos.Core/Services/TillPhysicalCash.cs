using WebPos.Core.Abstractions;
using WebPos.Core.Constants;

namespace WebPos.Core.Services;

/// <summary>
/// Books physical till float (opening cash / overage) into GL immediately before an outflow
/// so <c>CashAccount.BalancePaisa</c> and the non-negative CHECK stay valid.
/// </summary>
internal static class TillPhysicalCash
{
    public static async Task RecognizeIntoGlIfNeededAsync(
        ITransactionService transactions,
        long currentGlPaisa,
        string tillAccountCode,
        long outflowPaisa,
        Guid shiftId,
        string referenceNo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (outflowPaisa <= 0)
        {
            return;
        }

        long gap = outflowPaisa - Math.Max(0L, currentGlPaisa);
        if (gap <= 0)
        {
            return;
        }

        string trimmedRef = string.IsNullOrWhiteSpace(referenceNo)
            ? "FLOAT"
            : referenceNo.Trim();
        await transactions.PostBalancedEntriesAsync(
            new DoubleEntryPostRequest
            {
                TransactionType = "CASH_IN",
                ReferenceNo = $"{trimmedRef}-FLOAT",
                ReferenceDetails =
                    "Physical till float recognized for payout (opening float / overage).",
                ShiftId = shiftId,
                Postings =
                [
                    new LedgerPosting
                    {
                        AccountCode = tillAccountCode,
                        DebitPaisa = gap,
                        CreditPaisa = 0
                    },
                    new LedgerPosting
                    {
                        AccountCode = LedgerAccounts.UnregisteredCash,
                        DebitPaisa = 0,
                        CreditPaisa = gap
                    }
                ]
            },
            cancellationToken);
    }
}
