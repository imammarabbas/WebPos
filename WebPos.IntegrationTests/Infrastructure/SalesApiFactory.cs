using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the full WebPos pipeline with an isolated EF Core InMemory database per factory instance.
/// </summary>
public sealed class SalesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"WebPos_SalesApi_{Guid.NewGuid():N}";
    private readonly (string PrivateKeyPem, string PublicKeyPem) _enrollmentKeys =
        TestEnrollmentAuth.GenerateRsaKeyPair();

    public string PrivateKeyPem => _enrollmentKeys.PrivateKeyPem;

    public Guid DefaultTenantId => TestEnrollmentAuth.DefaultTenantId;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:PinHashKey"] =
                    "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                [TestEnrollmentAuth.PrivateKeyPemConfigKey] = _enrollmentKeys.PrivateKeyPem,
                [TestEnrollmentAuth.PublicKeyPemConfigKey] = _enrollmentKeys.PublicKeyPem,
                ["Security:Jwt:Key"] = "integration-test-jwt-signing-key-material-0123456789",
                // Allow enroll to rebind terminal tenant after restore-style mismatches.
                ["Security:AllowInsecureDevDefaults"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<WebPosDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddDbContextFactory<WebPosDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName),
                lifetime: ServiceLifetime.Scoped);
        });
    }

    /// <summary>
    /// Ensures master tenant exists for enrollment-scoped API calls.
    /// </summary>
    public async Task EnsureTenantAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        if (!await context.Tenants.AnyAsync(t => t.Id == DefaultTenantId))
        {
            context.Tenants.Add(new Tenant
            {
                Id = DefaultTenantId,
                Name = "Master Tenant",
                Slug = "master",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }
    }

    public void ApplyEnrollmentAuth(
        HttpRequestMessage request,
        Guid tenantId,
        Guid terminalId)
    {
        TestEnrollmentAuth.ApplyEnrollmentAuth(
            request,
            tenantId,
            terminalId,
            PrivateKeyPem);
    }

    /// <summary>
    /// Clears the isolated in-memory database between controller tests.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        ServiceDescriptor[] toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<WebPosDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(WebPosDbContext)
                || d.ServiceType == typeof(IDbContextFactory<WebPosDbContext>)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(WebPosDbContext)))
            .ToArray();

        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }

        services.RemoveAll(typeof(DbContextOptions<WebPosDbContext>));
        services.RemoveAll(typeof(WebPosDbContext));
        services.RemoveAll(typeof(IDbContextFactory<WebPosDbContext>));
    }
}
