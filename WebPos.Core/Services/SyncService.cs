using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class SyncService : ISyncService
{
    /// <summary>Bump when the sync payload shape changes incompatibly.</summary>
    public const string SchemaVersion = "1.1";

    /// <summary>Delta cursors older than this require a full bootstrap.</summary>
    public static readonly TimeSpan MaxDeltaWindow = TimeSpan.FromDays(30);

    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly ITenantService _tenantService;

    public SyncService(
        WebPosDbContext context,
        ITransactionService transactionService,
        ITenantService tenantService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<SyncBootstrapResponse> BootstrapAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        // Single ambient transaction => one consistent snapshot across all reads.
        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            List<SyncSupplierDto> suppliers = await _context.Parties
                .AsNoTracking()
                .Where(party =>
                    party.TenantId == tenantId
                    && party.PartyType == PartyTypes.Supplier
                    && !party.IsDeleted)
                .OrderBy(party => party.Name)
                .Select(party => new SyncSupplierDto
                {
                    Id = party.Id,
                    Name = party.Name,
                    PhoneNumber = party.PhoneNumber,
                    Address = party.Address,
                    CurrentBalancePaisa = party.CurrentBalancePaisa,
                    IsDeleted = false,
                    UpdatedAt = party.UpdatedAt
                })
                .ToListAsync(ct);

            List<SyncProductDto> products = await _context.Products
                .AsNoTracking()
                .Where(product =>
                    product.TenantId == tenantId
                    && !product.IsDeleted)
                .OrderBy(product => product.Name)
                .Select(product => new SyncProductDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Sku = product.Sku,
                    Barcode = product.Barcode,
                    ShortCode = product.ShortCode,
                    IsLoose = product.IsLoose,
                    BaseUnit = product.BaseUnit,
                    ConversionMultiplier = product.ConversionMultiplier,
                    IsDeleted = false,
                    UpdatedAt = product.UpdatedAt
                })
                .ToListAsync(ct);

            List<SyncShiftStatusDto> activeShifts = await _context.CashierShifts
                .AsNoTracking()
                .Where(shift =>
                    shift.TenantId == tenantId
                    && shift.Status == "OPEN")
                .Select(shift => new SyncShiftStatusDto
                {
                    ShiftId = shift.Id,
                    TerminalId = shift.TerminalId,
                    CashierId = shift.CashierId,
                    Status = shift.Status,
                    OpenedAt = shift.OpenedAt
                })
                .ToListAsync(ct);

            return new SyncBootstrapResponse
            {
                SchemaVersion = SchemaVersion,
                ServerTimeUtc = DateTimeOffset.UtcNow,
                Suppliers = suppliers,
                ActiveProducts = products,
                ActiveShifts = activeShifts
            };
        }, cancellationToken);
    }

    public async Task<SyncDeltaResponse> GetDeltaAsync(
        DateTimeOffset lastSyncUtc,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (lastSyncUtc > now.AddMinutes(5))
        {
            throw new InvalidOperationException(
                "lastSync must not be in the future.");
        }

        if (now - lastSyncUtc > MaxDeltaWindow)
        {
            throw new StaleSyncCursorException(
                $"lastSync is older than {MaxDeltaWindow.TotalDays:0} days. "
                + "Request a full bootstrap instead.");
        }

        // UpdatedAt cursor: strictly-after comparison; soft-deleted rows are included
        // so clients purge them from local stores.
        List<SyncSupplierDto> suppliers = await _context.Parties
            .AsNoTracking()
            .Where(party =>
                party.TenantId == tenantId
                && party.PartyType == PartyTypes.Supplier
                && party.UpdatedAt > lastSyncUtc)
            .OrderBy(party => party.UpdatedAt)
            .Select(party => new SyncSupplierDto
            {
                Id = party.Id,
                Name = party.Name,
                PhoneNumber = party.PhoneNumber,
                Address = party.Address,
                CurrentBalancePaisa = party.CurrentBalancePaisa,
                IsDeleted = party.IsDeleted,
                UpdatedAt = party.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        List<SyncProductDto> products = await _context.Products
            .AsNoTracking()
            .Where(product =>
                product.TenantId == tenantId
                && product.UpdatedAt > lastSyncUtc)
            .OrderBy(product => product.UpdatedAt)
            .Select(product => new SyncProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Sku = product.Sku,
                Barcode = product.Barcode,
                ShortCode = product.ShortCode,
                IsLoose = product.IsLoose,
                BaseUnit = product.BaseUnit,
                ConversionMultiplier = product.ConversionMultiplier,
                IsDeleted = product.IsDeleted,
                UpdatedAt = product.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new SyncDeltaResponse
        {
            SchemaVersion = SchemaVersion,
            ServerTimeUtc = now,
            Suppliers = suppliers,
            Products = products
        };
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required for sync operations.");
        }
    }
}
