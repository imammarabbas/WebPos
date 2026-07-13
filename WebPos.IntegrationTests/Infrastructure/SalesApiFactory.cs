using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebPos.Core.Data;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the full WebPos pipeline with an isolated EF Core InMemory database per factory instance.
/// </summary>
public sealed class SalesApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"WebPos_SalesApi_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<WebPosDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        ServiceDescriptor[] toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<WebPosDbContext>)
                || d.ServiceType == typeof(DbContextOptions)
                || d.ServiceType == typeof(WebPosDbContext)
                || (d.ServiceType.IsGenericType
                    && d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)
                    && d.ServiceType.GenericTypeArguments[0] == typeof(WebPosDbContext)))
            .ToArray();

        foreach (ServiceDescriptor descriptor in toRemove)
        {
            services.Remove(descriptor);
        }

        // Also strip any leftover EF provider options configured via AddDbContext factory delegates.
        services.RemoveAll(typeof(DbContextOptions<WebPosDbContext>));
        services.RemoveAll(typeof(WebPosDbContext));
    }
}
