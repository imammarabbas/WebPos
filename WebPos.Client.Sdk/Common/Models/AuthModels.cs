namespace Common.Models;

public sealed class LoginRequest
{
    public required string Pin { get; init; }
}

public sealed class CashierDto
{
    public Guid CashierId { get; init; }

    public required string Name { get; init; }
}
