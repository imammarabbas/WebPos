namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Terminal checkout labels mapped to API <c>paymentMethod</c> values.
/// </summary>
public static class CheckoutPaymentMethods
{
    public const string Cash = "CASH";
    public const string EasyPaisa = "EASYPAISA";
    public const string JazzCash = "JAZZCASH";
    public const string BankTransfer = "BANK_TRANSFER";

    /// <summary>Charge customer AR balance — requires a selected customer.</summary>
    public const string CustomerAccount = "CREDIT";

    public static IReadOnlyList<CheckoutPaymentOption> OnlineChannels { get; } =
    [
        new CheckoutPaymentOption(EasyPaisa, "Easypaisa"),
        new CheckoutPaymentOption(JazzCash, "JazzCash"),
        new CheckoutPaymentOption(BankTransfer, "Bank Transfer")
    ];

    public static bool RequiresCustomer(string paymentMethod) =>
        string.Equals(paymentMethod, CustomerAccount, StringComparison.OrdinalIgnoreCase);

    public static bool IsOnline(string paymentMethod) =>
        OnlineChannels.Any(channel =>
            string.Equals(channel.Method, paymentMethod, StringComparison.OrdinalIgnoreCase));

    public static bool IsCash(string paymentMethod) =>
        string.Equals(paymentMethod, Cash, StringComparison.OrdinalIgnoreCase);

    public static string DisplayLabel(string paymentMethod)
    {
        if (IsCash(paymentMethod))
        {
            return "Cash";
        }

        if (RequiresCustomer(paymentMethod))
        {
            return "Customer Account";
        }

        CheckoutPaymentOption? online = OnlineChannels.FirstOrDefault(channel =>
            string.Equals(channel.Method, paymentMethod, StringComparison.OrdinalIgnoreCase));
        return online?.Label ?? paymentMethod;
    }
}

public sealed record CheckoutPaymentOption(string Method, string Label);
