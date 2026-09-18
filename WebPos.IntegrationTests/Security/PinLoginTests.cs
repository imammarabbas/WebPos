using System.Diagnostics;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Security;

namespace WebPos.IntegrationTests.Security;

public sealed class PinLoginTests
{
    private const string Pin = "2468";
    private const string Key = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=";

    [Fact]
    public void HmacPinHasher_ShouldBeDeterministicAndFast()
    {
        HmacPinHasher hasher = CreateHasher();
        string hash = hasher.HashPin(Pin);

        hash.Should().NotContain(Pin);
        hasher.VerifyPin(Pin, hash).Should().BeTrue();
        hasher.VerifyPin("1357", hash).Should().BeFalse();

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool allVerified = true;
        for (int i = 0; i < 1_000; i++)
        {
            allVerified &= hasher.VerifyPin(Pin, hash);
        }

        stopwatch.Stop();
        allVerified.Should().BeTrue();
        stopwatch.Elapsed.Should().BeLessThan(
            TimeSpan.FromSeconds(1),
            "HMAC verification should average less than one millisecond");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnCashier_WhenPinHashMatches()
    {
        HmacPinHasher hasher = CreateHasher();
        (WebPosDbContext context, IDbContextFactory<WebPosDbContext> dbFactory) =
            CreateContextAndFactory();
        await using (context)
        {
            User cashier = await SeedUserAsync(context, "Cashier", pinHash: hasher.HashPin(Pin));
            LoginService service = new(dbFactory, hasher);

            Common.Models.CashierDto? result = await service.LoginAsync(Pin);

            result.Should().NotBeNull();
            result!.CashierId.Should().Be(cashier.Id);
            result.Name.Should().Be(cashier.Username);
        }
    }

    [Fact]
    public async Task LoginAsync_ShouldAllowManagerPin()
    {
        const string managerPin = "8642";
        HmacPinHasher hasher = CreateHasher();
        (WebPosDbContext context, IDbContextFactory<WebPosDbContext> dbFactory) =
            CreateContextAndFactory();
        await using (context)
        {
            User manager = await SeedUserAsync(
                context,
                "Manager",
                pinHash: hasher.HashPin(managerPin));
            LoginService service = new(dbFactory, hasher);

            Common.Models.CashierDto? result = await service.LoginAsync(managerPin);

            result.Should().NotBeNull();
            result!.CashierId.Should().Be(manager.Id);
        }
    }

    [Fact]
    public async Task LoginAsync_ShouldFallbackToPasswordHash_WhenPinHashIsEmpty()
    {
        HmacPinHasher hasher = CreateHasher();
        (WebPosDbContext context, IDbContextFactory<WebPosDbContext> dbFactory) =
            CreateContextAndFactory();
        await using (context)
        {
            User cashier = await SeedUserAsync(
                context,
                "Cashier",
                pinHash: string.Empty,
                passwordHash: CryptoHelper.HashPassword(Pin));
            LoginService service = new(dbFactory, hasher);

            Common.Models.CashierDto? result = await service.LoginAsync(Pin);

            result.Should().NotBeNull();
            result!.CashierId.Should().Be(cashier.Id);
        }
    }

    private static HmacPinHasher CreateHasher()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HmacPinHasher.ConfigurationKey] = Key
            })
            .Build();

        return new HmacPinHasher(configuration);
    }

    private static (WebPosDbContext Context, IDbContextFactory<WebPosDbContext> Factory)
        CreateContextAndFactory()
    {
        DbContextOptions<WebPosDbContext> options =
            new DbContextOptionsBuilder<WebPosDbContext>()
                .UseInMemoryDatabase($"WebPos_PinLogin_{Guid.NewGuid():N}")
                .Options;

        var tenantService = new FixedTenantService(TenantDefaults.MasterTenantId);
        var context = new WebPosDbContext(options, tenantService);
        IDbContextFactory<WebPosDbContext> factory =
            new TestDbContextFactory(options, tenantService);
        return (context, factory);
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }

    private static async Task<User> SeedUserAsync(
        WebPosDbContext context,
        string roleName,
        string pinHash,
        string? passwordHash = null)
    {
        Guid tenantId = TenantDefaults.MasterTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!await context.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Master Tenant",
                Slug = "master",
                IsActive = true,
                CreatedAt = now
            });
        }

        Role role = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RoleName = roleName,
            CreatedAt = now
        };
        User user = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}",
            PasswordHash = passwordHash ?? CryptoHelper.HashPassword("legacy-password"),
            PinHash = pinHash,
            RoleId = role.Id,
            Role = role,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        context.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class FixedTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
