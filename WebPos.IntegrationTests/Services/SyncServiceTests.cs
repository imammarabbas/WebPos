using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;
using WebPos.Filters;

namespace WebPos.IntegrationTests.Services;

public sealed class SyncServiceTests
{
    [Fact]
    public async Task BootstrapAsync_ShouldReturnTenantScopedStateWithoutSoftDeleted()
    {
        await using SyncHarness harness = await SyncHarness.CreateAsync();
        await harness.SeedAsync();

        SyncBootstrapResponse response = await harness.SyncService.BootstrapAsync();

        response.SchemaVersion.Should().Be(SyncService.SchemaVersion);
        response.Suppliers.Should().ContainSingle(s => s.Name == "Active Supplier");
        response.Suppliers.Should().NotContain(s => s.Name == "Deleted Supplier");
        response.ActiveProducts.Should().ContainSingle(p => p.Name == "Active Product");
        response.ActiveProducts.Should().NotContain(p => p.Name == "Deleted Product");
        response.ActiveShifts.Should().ContainSingle(s => s.Status == "OPEN");
    }

    [Fact]
    public async Task GetDeltaAsync_ShouldReturnOnlyChangesIncludingSoftDeleted()
    {
        await using SyncHarness harness = await SyncHarness.CreateAsync();
        DateTimeOffset cursor = DateTimeOffset.UtcNow.AddHours(-1);
        await harness.SeedAsync(
            staleTimestamp: cursor.AddHours(-2),
            freshTimestamp: cursor.AddMinutes(30));

        SyncDeltaResponse delta = await harness.SyncService.GetDeltaAsync(cursor);

        // Fresh rows (updated after cursor) are returned — including the tombstones.
        delta.Suppliers.Should().ContainSingle(s => s.Name == "Deleted Supplier")
            .Which.IsDeleted.Should().BeTrue();
        delta.Products.Should().ContainSingle(p => p.Name == "Deleted Product")
            .Which.IsDeleted.Should().BeTrue();
        // Stale rows (updated before cursor) are excluded.
        delta.Suppliers.Should().NotContain(s => s.Name == "Active Supplier");
        delta.Products.Should().NotContain(p => p.Name == "Active Product");
    }

    [Fact]
    public async Task GetDeltaAsync_ShouldRejectCursorOlderThanWindow()
    {
        await using SyncHarness harness = await SyncHarness.CreateAsync();

        Func<Task> act = () => harness.SyncService.GetDeltaAsync(
            DateTimeOffset.UtcNow.AddDays(-31));

        // StaleSyncCursorException derives from InvalidOperationException,
        // which GlobalExceptionHandler maps to 400 Bad Request.
        await act.Should().ThrowAsync<StaleSyncCursorException>()
            .WithMessage("*full bootstrap*");
    }

    [Fact]
    public void SchemaVersionFilter_ShouldReject406_ForIncompatibleClient()
    {
        ActionExecutingContext context = CreateActionContext(clientVersion: "2.0");

        new SyncSchemaVersionFilter().OnActionExecuting(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status406NotAcceptable);
    }

    [Fact]
    public void SchemaVersionFilter_ShouldStampHeaderAndPass_ForCompatibleClient()
    {
        ActionExecutingContext context = CreateActionContext(clientVersion: "1.3");

        new SyncSchemaVersionFilter().OnActionExecuting(context);

        context.Result.Should().BeNull();
        context.HttpContext.Response.Headers[SyncSchemaVersionFilter.HeaderName]
            .ToString().Should().Be(SyncService.SchemaVersion);
    }

    private static ActionExecutingContext CreateActionContext(string? clientVersion)
    {
        DefaultHttpContext httpContext = new();
        if (clientVersion is not null)
        {
            httpContext.Request.Headers[SyncSchemaVersionFilter.HeaderName] =
                clientVersion;
        }

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            [],
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private sealed class SyncHarness : IAsyncDisposable
    {
        private SyncHarness(WebPosDbContext context, ISyncService syncService, Guid tenantId)
        {
            Context = context;
            SyncService = syncService;
            TenantId = tenantId;
        }

        public WebPosDbContext Context { get; }

        public ISyncService SyncService { get; }

        public Guid TenantId { get; }

        public static async Task<SyncHarness> CreateAsync()
        {
            Guid tenantId = TenantDefaults.MasterTenantId;
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase($"WebPos_Sync_{Guid.NewGuid():N}")
                    .Options;

            var tenantService = new TestTenantService(tenantId);
            var context = new WebPosDbContext(options, tenantService);
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = $"master-{tenantId:N}",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();

            ISyncService syncService = new SyncService(
                context,
                new TransactionService(context),
                tenantService);

            return new SyncHarness(context, syncService, tenantId);
        }

        public async Task SeedAsync(
            DateTimeOffset? staleTimestamp = null,
            DateTimeOffset? freshTimestamp = null)
        {
            DateTimeOffset stale = staleTimestamp ?? DateTimeOffset.UtcNow.AddHours(-3);
            DateTimeOffset fresh = freshTimestamp ?? DateTimeOffset.UtcNow;

            Guid roleId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();
            Guid terminalId = Guid.NewGuid();

            Context.Parties.AddRange(
                new Party
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PartyType = PartyTypes.Supplier,
                    Name = "Active Supplier",
                    PhoneNumber = "0300-1",
                    Address = "",
                    IsDeleted = false,
                    CreatedAt = stale,
                    UpdatedAt = stale
                },
                new Party
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PartyType = PartyTypes.Supplier,
                    Name = "Deleted Supplier",
                    PhoneNumber = "0300-2",
                    Address = "",
                    IsDeleted = true,
                    CreatedAt = stale,
                    UpdatedAt = fresh
                },
                new Party
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    PartyType = PartyTypes.Customer,
                    Name = "Some Customer",
                    PhoneNumber = "0300-3",
                    Address = "",
                    IsDeleted = false,
                    CreatedAt = stale,
                    UpdatedAt = fresh
                });

            Context.Products.AddRange(
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    Name = "Active Product",
                    Sku = "SKU-A",
                    Barcode = "BAR-A",
                    Brand = "WebPos",
                    BaseUnit = "PCS",
                    ConversionMultiplier = 1,
                    IsDeleted = false,
                    CreatedAt = stale,
                    UpdatedAt = stale
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    Name = "Deleted Product",
                    Sku = "SKU-D",
                    Barcode = "BAR-D",
                    Brand = "WebPos",
                    BaseUnit = "PCS",
                    ConversionMultiplier = 1,
                    IsDeleted = true,
                    CreatedAt = stale,
                    UpdatedAt = fresh
                });

            Context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = TenantId,
                RoleName = "Cashier",
                CreatedAt = stale
            });
            Context.Users.Add(new User
            {
                Id = userId,
                TenantId = TenantId,
                Username = $"sync-{userId:N}"[..20],
                PasswordHash = "x",
                RoleId = roleId,
                IsActive = true,
                CreatedAt = stale,
                UpdatedAt = stale
            });
            Context.Terminals.Add(new Terminal
            {
                Id = terminalId,
                TenantId = TenantId,
                TerminalName = $"T-{terminalId:N}"[..20],
                MacAddress = $"M-{terminalId:N}"[..20],
                IsActive = true,
                LastSyncTime = stale
            });
            Context.CashierShifts.AddRange(
                new CashierShift
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TerminalId = terminalId,
                    CashierId = userId,
                    OpenedAt = stale,
                    Status = "OPEN"
                },
                new CashierShift
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    TerminalId = terminalId,
                    CashierId = userId,
                    OpenedAt = stale.AddDays(-1),
                    ClosedAt = stale,
                    Status = "CLOSED"
                });

            await Context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
