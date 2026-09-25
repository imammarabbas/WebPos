namespace WebPos.WindowsTerminal.Services;

public enum PaymentBreakdownMode
{
    Cash,
    Online,
    Credit
}

public sealed record PaymentBreakdown(
    bool IsWalkIn,
    long PreviousDuePaisa,
    long CurrentSaleTotalPaisa,
    long AmountDuePaisa,
    long PaymentReceivedPaisa,
    long ChangeReturnPaisa,
    long OnAccountPaisa,
    long AccountCreditPaisa,
    long NewCustomerBalancePaisa,
    string ChangeLabel,
    bool CanConfirm,
    bool ApplyExcessAsCustomerCredit);

/// <summary>
/// Payment Received / Change / Account Credit / CFD ledger math (paisa).
/// Registered overpay always credits the customer account (never change).
/// </summary>
public static class PaymentBreakdownCalculator
{
    public const string ChangeToReturnLabel = "CHANGE TO RETURN";
    public const string AccountCreditLabel = "Account Credit";
    public const string NewBalanceLabel = "New Balance";

    public static PaymentBreakdown Compute(
        bool isWalkIn,
        long previousDuePaisa,
        long currentSaleTotalPaisa,
        long paymentReceivedPaisa,
        PaymentBreakdownMode mode,
        bool applyExcessAsCustomerCredit)
    {
        _ = applyExcessAsCustomerCredit; // registered excess always credits account

        long sale = Math.Max(0L, currentSaleTotalPaisa);
        // Keep signed AR: positive = customer owes, negative = customer credit on account.
        long previousDue = isWalkIn ? 0L : previousDuePaisa;
        long totalLiability = previousDue + sale;

        if (mode == PaymentBreakdownMode.Credit)
        {
            if (isWalkIn)
            {
                return new PaymentBreakdown(
                    IsWalkIn: true,
                    PreviousDuePaisa: 0,
                    CurrentSaleTotalPaisa: sale,
                    AmountDuePaisa: sale,
                    PaymentReceivedPaisa: 0,
                    ChangeReturnPaisa: 0,
                    OnAccountPaisa: 0,
                    AccountCreditPaisa: 0,
                    NewCustomerBalancePaisa: 0,
                    ChangeLabel: ChangeToReturnLabel,
                    CanConfirm: false,
                    ApplyExcessAsCustomerCredit: false);
            }

            return new PaymentBreakdown(
                IsWalkIn: false,
                PreviousDuePaisa: previousDue,
                CurrentSaleTotalPaisa: sale,
                AmountDuePaisa: totalLiability,
                PaymentReceivedPaisa: 0,
                ChangeReturnPaisa: 0,
                OnAccountPaisa: sale,
                AccountCreditPaisa: 0,
                NewCustomerBalancePaisa: previousDue + sale,
                ChangeLabel: "On Account",
                CanConfirm: true,
                ApplyExcessAsCustomerCredit: false);
        }

        long paid = Math.Max(0L, paymentReceivedPaisa);

        if (isWalkIn)
        {
            bool canConfirm = paid >= sale;
            long change = canConfirm ? paid - sale : 0L;
            return new PaymentBreakdown(
                IsWalkIn: true,
                PreviousDuePaisa: 0,
                CurrentSaleTotalPaisa: sale,
                AmountDuePaisa: sale,
                PaymentReceivedPaisa: paid,
                ChangeReturnPaisa: change,
                OnAccountPaisa: 0,
                AccountCreditPaisa: 0,
                NewCustomerBalancePaisa: 0,
                ChangeLabel: ChangeToReturnLabel,
                CanConfirm: canConfirm,
                ApplyExcessAsCustomerCredit: false);
        }

        // Registered Cash / Online:
        // underpay → remainder on ledger; overpay → excess always to account (cash box, no change).
        // Account Credit is a separate box; ChangeLabel stays "New Balance".
        long newBalance = previousDue + sale - paid;
        long accountCredit = Math.Max(0L, paid - totalLiability);
        bool applyCredit = accountCredit > 0;

        return new PaymentBreakdown(
            IsWalkIn: false,
            PreviousDuePaisa: previousDue,
            CurrentSaleTotalPaisa: sale,
            AmountDuePaisa: totalLiability,
            PaymentReceivedPaisa: paid,
            ChangeReturnPaisa: 0,
            OnAccountPaisa: 0,
            AccountCreditPaisa: accountCredit,
            NewCustomerBalancePaisa: newBalance,
            ChangeLabel: NewBalanceLabel,
            CanConfirm: true,
            ApplyExcessAsCustomerCredit: applyCredit);
    }
}
