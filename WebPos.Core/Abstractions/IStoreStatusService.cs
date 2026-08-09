namespace WebPos.Core.Abstractions;

public sealed class StoreStatusDto
{
    public bool TillOpen { get; init; }

    public bool ApiOnline { get; init; } = true;

    public int OpenShiftCount { get; init; }

    public string StoreName { get; init; } = string.Empty;

    public string PosDisplayName { get; init; } = string.Empty;
}

public interface IStoreStatusService
{
    Task<StoreStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
