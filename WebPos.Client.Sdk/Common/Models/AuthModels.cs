namespace Common.Models;

public sealed class LoginRequest
{
    public required string Pin { get; init; }

    /// <summary>Optional role filter: Cashier, Manager, Owner, or Admin.</summary>
    public string? Role { get; init; }
}

/// <summary>Terminal staff login using the same username/password as the master site.</summary>
public sealed class StaffPasswordLoginRequest
{
    public required string Username { get; init; }

    public required string Password { get; init; }
}

public sealed class CashierDto
{
    public Guid CashierId { get; init; }

    public required string Name { get; init; }

    public string RoleName { get; init; } = "Cashier";
}

public sealed class CashierSalesSummaryDto
{
    public Guid CashierId { get; init; }

    public required string CashierName { get; init; }

    public required string RoleName { get; init; }

    public long SalesPaisa { get; init; }

    public int SaleCount { get; init; }

    public long ReturnsPaisa { get; init; }

    public int ReturnCount { get; init; }

    public long NetSalesPaisa => SalesPaisa - ReturnsPaisa;
}
