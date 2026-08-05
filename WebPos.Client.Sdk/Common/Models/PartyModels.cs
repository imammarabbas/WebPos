namespace Common.Models;

public sealed class PartyDto
{
    public Guid Id { get; init; }

    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }

    public long CurrentBalancePaisa { get; init; }
}

public sealed class CreatePartyRequest
{
    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}
