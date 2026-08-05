namespace WebPos.Core.Constants;

public static class PartyTypes
{
    public const string Customer = "CUSTOMER";
    public const string Supplier = "SUPPLIER";

    public static bool IsKnown(string? partyType) =>
        string.Equals(partyType, Customer, StringComparison.OrdinalIgnoreCase)
        || string.Equals(partyType, Supplier, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string partyType) =>
        partyType.Trim().ToUpperInvariant();
}
