namespace WebPos.Core.Abstractions;

/// <summary>
/// Thrown when the client's sync cursor is older than the supported delta window;
/// the client must perform a full bootstrap. Maps to 400 Bad Request.
/// </summary>
public sealed class StaleSyncCursorException(string message)
    : InvalidOperationException(message);

public sealed class SyncSupplierDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string PhoneNumber { get; init; }

    public string Address { get; init; } = string.Empty;

    public long CurrentBalancePaisa { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class SyncProductDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Sku { get; init; }

    public required string Barcode { get; init; }

    public required string ShortCode { get; init; }

    public bool IsLoose { get; init; }

    public string BaseUnit { get; init; } = string.Empty;

    public int ConversionMultiplier { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class SyncShiftStatusDto
{
    public required Guid ShiftId { get; init; }

    public required Guid TerminalId { get; init; }

    public required Guid CashierId { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset OpenedAt { get; init; }
}

public sealed class SyncBootstrapResponse
{
    public required string SchemaVersion { get; init; }

    public required DateTimeOffset ServerTimeUtc { get; init; }

    public required IReadOnlyList<SyncSupplierDto> Suppliers { get; init; }

    public required IReadOnlyList<SyncProductDto> ActiveProducts { get; init; }

    public required IReadOnlyList<SyncShiftStatusDto> ActiveShifts { get; init; }
}

public sealed class SyncDeltaResponse
{
    public required string SchemaVersion { get; init; }

    public required DateTimeOffset ServerTimeUtc { get; init; }

    /// <summary>Changed since the cursor — includes soft-deleted rows (IsDeleted = true).</summary>
    public required IReadOnlyList<SyncSupplierDto> Suppliers { get; init; }

    public required IReadOnlyList<SyncProductDto> Products { get; init; }
}

public interface ISyncService
{
    /// <summary>Full state snapshot inside a single read transaction.</summary>
    Task<SyncBootstrapResponse> BootstrapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Entities changed strictly after <paramref name="lastSyncUtc"/> (UpdatedAt cursor),
    /// including soft-deleted rows. Throws <see cref="StaleSyncCursorException"/> when the
    /// cursor is older than the supported window.
    /// </summary>
    Task<SyncDeltaResponse> GetDeltaAsync(
        DateTimeOffset lastSyncUtc,
        CancellationToken cancellationToken = default);
}
