namespace WebPos.Core.Constants;

public static class ShiftCashMovementDirections
{
    public const string In = "IN";
    public const string Out = "OUT";
}

public static class ShiftCashMovementStatuses
{
    public const string Unreconciled = "UNRECONCILED";
    public const string Reconciled = "RECONCILED";
    public const string Reversed = "REVERSED";
}

public static class ShiftCashInReasons
{
    public const string UnregisteredCash = "Unregistered Cash";
    public const string OwnerCashAdded = "Owner Cash Added";
    public const string FloatTopUp = "Float Top-up";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        UnregisteredCash,
        OwnerCashAdded,
        FloatTopUp,
        Other
    ];
}

public static class ShiftCashOutReasons
{
    public const string UnregisteredPayment = "Unregistered Payment";
    public const string OwnerWithdrawal = "Owner Withdrawal";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        UnregisteredPayment,
        OwnerWithdrawal,
        Other
    ];
}
