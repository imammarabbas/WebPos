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

    public static string Expense(string category) =>
        $"EXPENSE:{category.ToUpperInvariant()}";

    public static string PaymentMethodToAccount(string paymentMethod) =>
        paymentMethod.ToUpperInvariant() switch
        {
            "CASH" => Cash,
            "EASYPAISA" => EasyPaisa,
            "JAZZCASH" => JazzCash,
            "BANK_TRANSFER" => Bank,
            "CREDIT" => AccountsReceivable,
            _ => Cash
        };

    public static bool IsCashAccount(string accountCode) =>
        string.Equals(accountCode, Cash, StringComparison.OrdinalIgnoreCase);
}
