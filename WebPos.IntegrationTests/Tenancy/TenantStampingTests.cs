using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.IntegrationTests.Tenancy;

public sealed class TenantStampingTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldStampAddedEntity_WithResolvedTenant()
    {
        Guid tenantId = Guid.NewGuid();
        await using WebPosDbContext context = CreateContext(
            new TestTenantService(tenantId, isResolved: true));
        Product product = CreateProduct();
        context.Products.Add(product);

        await context.SaveChangesAsync();

        product.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPreserveExplicitTenant_ForImport()
    {
        Guid importTenantId = Guid.NewGuid();
        await using WebPosDbContext context = CreateContext(
            new TestTenantService(Guid.Empty, isResolved: false));
        Product product = CreateProduct();
        product.TenantId = importTenantId;
        context.Products.Add(product);

        await context.SaveChangesAsync();

        product.TenantId.Should().Be(importTenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldThrow_WhenTenantIsNotResolved()
    {
        await using WebPosDbContext context = CreateContext(
            new TestTenantService(Guid.Empty, isResolved: false));
        context.Products.Add(CreateProduct());

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*without a resolved TenantId*");
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldThrow_WhenExplicitTenantMismatchesRequest()
    {
        await using WebPosDbContext context = CreateContext(
            new TestTenantService(Guid.NewGuid(), isResolved: true));
        Product product = CreateProduct();
        product.TenantId = Guid.NewGuid();
        context.Products.Add(product);

        Func<Task> act = () => context.SaveChangesAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*different tenant*");
    }

    private static WebPosDbContext CreateContext(ITenantService tenantService)
    {
        DbContextOptions<WebPosDbContext> options =
            new DbContextOptionsBuilder<WebPosDbContext>()
                .UseInMemoryDatabase($"WebPos_TenantStamping_{Guid.NewGuid():N}")
                .Options;

        return new WebPosDbContext(options, tenantService);
    }

    private static Product CreateProduct() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Product",
            Sku = $"SKU-{Guid.NewGuid():N}",
            Barcode = Guid.NewGuid().ToString("N"),
            Brand = "Test",
            BaseUnit = "PCS",
            ConversionMultiplier = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private sealed class TestTenantService(Guid tenantId, bool isResolved) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved { get; } = isResolved;
    }
}
