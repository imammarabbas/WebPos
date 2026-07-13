namespace WebPos.Core.Constants;

public static class ExpenseCategories
{
    public const string Maintenance = "MAINTENANCE";
    public const string Rent = "RENT";
    public const string Wages = "WAGES";
    public const string Electricity = "ELECTRICITY";
    public const string Damage = "DAMAGE";
    public const string Theft = "THEFT";
    public const string OwnerDraw = "OWNER_DRAW";
    public const string Discount = "DISCOUNT";
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> All =
    [
        Maintenance,
        Rent,
        Wages,
        Electricity,
        Damage,
        Theft,
        OwnerDraw,
        Discount,
        Other
    ];

    public static bool RequiresReceiptReference(string category) =>
        string.Equals(category, Maintenance, StringComparison.OrdinalIgnoreCase);
}
