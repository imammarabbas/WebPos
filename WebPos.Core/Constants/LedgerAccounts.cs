namespace WebPos.Core.Constants;

public static class LedgerAccounts
{
    public const string Cash = "CASH";
    public const string Bank = "BANK";
    public const string EasyPaisa = "EASYPAISA";
    public const string JazzCash = "JAZZCASH";
    public const string Revenue = "REVENUE";
    public const string AccountsReceivable = "ACCOUNTS_RECEIVABLE";
    public const string AccountsPayable = "ACCOUNTS_PAYABLE";
    public const string Inventory = "INVENTORY";

    /// <summary>Holding account for Cash In excess / Cash Out without a business document.</summary>
    public const string UnregisteredCash = "LIABILITY:UNREGISTERED_CASH";

    /// <summary>Owner capital contribution (not sale revenue).</summary>
    public const string OwnerCapital = "EQUITY:OWNER_CAPITAL";

    /// <summary>Loan payable liability (not expense / not revenue).</summary>
    public const string LoanPayable = "LIABILITY:LOAN";

    public static string Expense(string category) =>
        $"EXPENSE:{category.ToUpperInvariant()}";

    public static string PaymentMethodToAccount(string paymentMethod) =>
        paymentMethod.ToUpperInvariant() switch
        {
            "CASH" => Cash,
            "CREDIT_CARD" => Bank,
            "EASYPAISA" => EasyPaisa,
            "JAZZCASH" => JazzCash,
            "BANK_TRANSFER" => Bank,
            "CREDIT" => AccountsReceivable,
            _ => Cash
        };

    public static bool IsCashAccount(string accountCode) =>
        string.Equals(accountCode, Cash, StringComparison.OrdinalIgnoreCase)
        || accountCode.StartsWith("CASH:TILL:", StringComparison.OrdinalIgnoreCase);
}
