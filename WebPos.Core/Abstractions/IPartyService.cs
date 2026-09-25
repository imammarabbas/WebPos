namespace WebPos.Core.Abstractions;

public sealed class CreatePartyRequest
{
    /// <summary>Maps to Party.PartyType — CUSTOMER or SUPPLIER.</summary>
    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}

public sealed class UpdatePartyRequest
{
    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }
}

public sealed class PartyDto
{
    public required Guid Id { get; init; }

    public required string Role { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CreditLimitPaisa { get; init; }

    public long CurrentBalancePaisa { get; init; }
}

public interface IPartyService
{
    Task<PartyDto> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyDto> UpdatePartyAsync(
        Guid partyId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyDto>> GetPartiesAsync(
        string? role,
        CancellationToken cancellationToken = default);

    Task<PartyDto> GetPartyAsync(
        Guid partyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the supplier for the current tenant or throws if missing/wrong type.
    /// </summary>
    Task<PartyDto> GetSupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyDto>> GetSuppliersAsync(
        CancellationToken cancellationToken = default);
}
