namespace WebPos.Core.Constants;

/// <summary>Cash / treasury account kinds for the dynamic CashAccount registry.</summary>
public enum CashAccountType
{
    Till = 0,
    Bank = 1,
    Mobile = 2,
    Petty = 3,
    Owner = 4,
    Other = 5
}

public static class CashAccountTypes
{
    public const string Till = "TILL";
    public const string Bank = "BANK";
    public const string Mobile = "MOBILE";
    public const string Petty = "PETTY";
    public const string Owner = "OWNER";
    public const string Other = "OTHER";

    public static string ToStored(CashAccountType type) =>
        type switch
        {
            CashAccountType.Till => Till,
            CashAccountType.Bank => Bank,
            CashAccountType.Mobile => Mobile,
            CashAccountType.Petty => Petty,
            CashAccountType.Owner => Owner,
            CashAccountType.Other => Other,
            _ => Other
        };

    public static CashAccountType FromStored(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            Till => CashAccountType.Till,
            Bank => CashAccountType.Bank,
            Mobile => CashAccountType.Mobile,
            Petty => CashAccountType.Petty,
            Owner => CashAccountType.Owner,
            _ => CashAccountType.Other
        };
}
