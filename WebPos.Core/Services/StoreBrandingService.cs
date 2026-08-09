using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Seeding;

namespace WebPos.Core.Services;

public sealed class StoreBrandingService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    ITenantService tenantService) : IStoreBrandingService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));

    public async Task<StoreBrandingDto> GetAsync(CancellationToken cancellationToken = default)
    {
        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Tenant tenant = await ResolveTenantAsync(context, cancellationToken);
        return ToDto(tenant);
    }

    public async Task<StoreBrandingDto> UpdateAsync(
        UpdateStoreBrandingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string storeName = request.StoreName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(storeName))
        {
            throw new ArgumentException("Store name is required.", nameof(request));
        }

        if (storeName.Length > 100)
        {
            throw new ArgumentException("Store name is too long (max 100).", nameof(request));
        }

        string posName = string.IsNullOrWhiteSpace(request.PosDisplayName)
            ? storeName
            : request.PosDisplayName.Trim();
        if (posName.Length > 100)
        {
            throw new ArgumentException("POS display name is too long (max 100).", nameof(request));
        }

        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        Tenant tenant = await ResolveTenantAsync(context, cancellationToken);
        tenant.Name = storeName;
        tenant.PosDisplayName = posName;
        await context.SaveChangesAsync(cancellationToken);
        return ToDto(tenant);
    }

    private Task<Tenant> ResolveTenantAsync(WebPosDbContext context, CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.IsResolved && _tenantService.TenantId != Guid.Empty
            ? _tenantService.TenantId
            : TenantDefaults.MasterTenantId;

        return ResolveTenantCoreAsync(context, tenantId, cancellationToken);
    }

    private static async Task<Tenant> ResolveTenantCoreAsync(
        WebPosDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        Tenant? tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant is not null)
        {
            return tenant;
        }

        return await context.Tenants
            .IgnoreQueryFilters()
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No tenant found.");
    }

    private static StoreBrandingDto ToDto(Tenant tenant)
    {
        string store = string.IsNullOrWhiteSpace(tenant.Name)
            ? PilotDataSeeder.StoreName
            : tenant.Name;
        string pos = string.IsNullOrWhiteSpace(tenant.PosDisplayName) ? store : tenant.PosDisplayName;
        return new StoreBrandingDto
        {
            StoreName = store,
            PosDisplayName = pos
        };
    }
}
