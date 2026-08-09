namespace WebPos.Core.Abstractions;

public sealed class StoreBrandingDto
{
    public required string StoreName { get; init; }

    public required string PosDisplayName { get; init; }
}

public sealed class UpdateStoreBrandingRequest
{
    public required string StoreName { get; init; }

    public string? PosDisplayName { get; init; }
}

public interface IStoreBrandingService
{
    Task<StoreBrandingDto> GetAsync(CancellationToken cancellationToken = default);

    Task<StoreBrandingDto> UpdateAsync(
        UpdateStoreBrandingRequest request,
        CancellationToken cancellationToken = default);
}
