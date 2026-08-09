using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class StoreStatusService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    ITenantService tenantService) : IStoreStatusService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));

    public async Task<StoreStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant context is required for store status.");
        }

        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        int openShiftCount = await context.CashierShifts.CountAsync(
            s => s.Status == "OPEN",
            cancellationToken);

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == _tenantService.TenantId)
            .Select(t => new { t.Name, t.PosDisplayName })
            .FirstOrDefaultAsync(cancellationToken);

        string storeName = string.IsNullOrWhiteSpace(tenant?.Name)
            ? Seeding.PilotDataSeeder.StoreName
            : tenant!.Name;
        string posName = string.IsNullOrWhiteSpace(tenant?.PosDisplayName)
            ? storeName
            : tenant!.PosDisplayName;

        return new StoreStatusDto
        {
            TillOpen = openShiftCount > 0,
            ApiOnline = true,
            OpenShiftCount = openShiftCount,
            StoreName = storeName,
            PosDisplayName = posName
        };
    }
}
