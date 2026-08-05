namespace WebPos.Core.Constants;

/// <summary>API-facing party ledger reference types mapped from stored transaction_type.</summary>
public static class PartyLedgerReferenceTypes
{
    public const string Sale = "Sale";
    public const string Purchase = "Purchase";
    public const string PaymentIn = "PaymentIn";
    public const string PaymentOut = "PaymentOut";
    public const string CustomerReturn = "CustomerReturn";
    public const string SupplierReturn = "SupplierReturn";
    public const string Opening = "Opening";
    public const string Adjustment = "Adjustment";

    public static string FromStored(string type, string partyType)
    {
        string t = type.ToUpperInvariant();
        string p = partyType.ToUpperInvariant();
        return t switch
        {
            "SALE" => Sale,
            "PURCHASE" => Purchase,
            "PAYMENT" when p == PartyTypes.Customer => PaymentIn,
            "PAYMENT" when p == PartyTypes.Supplier => PaymentOut,
            "CUSTOMER_RETURN" => CustomerReturn,
            "SUPPLIER_RETURN" => SupplierReturn,
            "OPENING" => Opening,
            "ADJUSTMENT" => Adjustment,
            _ => type
        };
    }
}
