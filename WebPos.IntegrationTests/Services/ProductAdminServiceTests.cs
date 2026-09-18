using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.IntegrationTests.Services;

public sealed class ProductAdminServiceTests
{
    [Fact]
    public async Task CreateAlias_WhenParentIsIndependent_Throws()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto independent = await harness.Service.CreateAsync(Master("Retail Sugar"));

        Func<Task> act = () => harness.Service.CreateAsync(Alias("Sugar 50g", independent.Id, 50m));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*bulk warehouse*");
    }

    [Fact]
    public async Task CreateAlias_WhenParentIsBulk_Succeeds()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto bulk = await harness.Service.CreateAsync(Master("Bulk Sugar", isBulk: true, baseUnit: "g"));

        ProductAdminDto alias = await harness.Service.CreateAsync(Alias("Sugar 50g", bulk.Id, 50m));

        alias.ParentProductId.Should().Be(bulk.Id);
        alias.IsBulk.Should().BeFalse();
        alias.DeductionMultiplier.Should().Be(50m);
    }

    [Fact]
    public async Task CreateAlias_WhenParentIsAnotherAlias_Throws()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto bulk = await harness.Service.CreateAsync(Master("Bulk Flour", isBulk: true, baseUnit: "g"));
        ProductAdminDto child = await harness.Service.CreateAsync(Alias("Flour 50g", bulk.Id, 50m));

        Func<Task> act = () => harness.Service.CreateAsync(Alias("Nested", child.Id, 1m));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*one level only*");
    }

    [Fact]
    public async Task Update_WhenClearingBulkWhileChildrenExist_Throws()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto bulk = await harness.Service.CreateAsync(Master("Bulk Rice", isBulk: true, baseUnit: "g"));
        await harness.Service.CreateAsync(Alias("Rice 50g", bulk.Id, 50m));

        Func<Task> act = () => harness.Service.UpdateAsync(bulk.Id, Master("Bulk Rice", isBulk: false, baseUnit: "g"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*child variants*");
    }

    [Fact]
    public async Task SearchAsync_PagesResultsWithoutReturningAllRows()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        await harness.Service.CreateAsync(Master("Alpha Rice"));
        await harness.Service.CreateAsync(Master("Beta Flour"));
        await harness.Service.CreateAsync(Master("Gamma Oil"));

        PagedResult<ProductAdminDto> page1 = await harness.Service.SearchAsync(new ProductSearchQuery
        {
            Page = 1,
            PageSize = 2
        });

        page1.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page1.Items.Select(p => p.Name).Should().Equal("Alpha Rice", "Beta Flour");

        PagedResult<ProductAdminDto> page2 = await harness.Service.SearchAsync(new ProductSearchQuery
        {
            Page = 2,
            PageSize = 2
        });
        page2.Items.Should().HaveCount(1);
        page2.Items[0].Name.Should().Be("Gamma Oil");
    }

    [Fact]
    public async Task SearchAsync_MatchesBarcode()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        await harness.Service.CreateAsync(Master("Hidden"));
        ProductAdminDto tagged = await harness.Service.CreateAsync(new UpsertProductRequest
        {
            Name = "Barcode Hit",
            Sku = string.Empty,
            Barcode = "8901234567890"
        });

        PagedResult<ProductAdminDto> result = await harness.Service.SearchAsync(new ProductSearchQuery
        {
            Search = "8901234567890",
            Page = 1,
            PageSize = 50
        });

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(p => p.Id == tagged.Id);
    }

    [Fact]
    public async Task ListBulkParentsAsync_OmitsIndependentsAndChildren()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto bulk = await harness.Service.CreateAsync(Master("Bulk Wheat", isBulk: true, baseUnit: "g"));
        await harness.Service.CreateAsync(Master("Retail Snack"));
        await harness.Service.CreateAsync(Alias("Wheat 50g", bulk.Id, 50m));

        IReadOnlyList<ProductAdminDto> parents = await harness.Service.ListBulkParentsAsync(search: null);

        parents.Should().ContainSingle();
        parents[0].Id.Should().Be(bulk.Id);
        parents[0].IsBulk.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_LowStockIncludesAliasAvailability()
    {
        await using ProductAdminHarness harness = await ProductAdminHarness.CreateAsync();
        ProductAdminDto bulk = await harness.Service.CreateAsync(new UpsertProductRequest
        {
            Name = "Bulk Lentils",
            Sku = string.Empty,
            IsBulk = true,
            BaseUnit = "g",
            MinStockQty = 1m
        });
        Product product = await harness.Context.Products.SingleAsync(p => p.Id == bulk.Id);
        product.StockQty = 80m;
        await harness.Context.SaveChangesAsync();
        ProductAdminDto alias = await harness.Service.CreateAsync(new UpsertProductRequest
        {
            Name = "Lentils 50g",
            Sku = string.Empty,
            ParentProductId = bulk.Id,
            DeductionMultiplier = 50m,
            MinStockQty = 2m
        });

        PagedResult<ProductAdminDto> result = await harness.Service.SearchAsync(new ProductSearchQuery
        {
            LowStockOnly = true,
            Page = 1,
            PageSize = 50
        });

        result.Items.Should().Contain(p => p.Id == alias.Id);
        result.Items.Single(p => p.Id == alias.Id).AvailableStock.Should().Be(1m);
    }

    private static UpsertProductRequest Master(string name, bool isBulk = false, string baseUnit = "PCS") =>
        new()
        {
            Name = name,
            Sku = string.Empty,
            Barcode = string.Empty,
            IsBulk = isBulk,
            BaseUnit = baseUnit
        };

    private static UpsertProductRequest Alias(string name, Guid parentId, decimal multiplier) =>
        new()
        {
            Name = name,
            Sku = string.Empty,
            Barcode = string.Empty,
            ParentProductId = parentId,
            DeductionMultiplier = multiplier,
            BaseUnit = "PCS"
        };

    private sealed class ProductAdminHarness : IAsyncDisposable
    {
        private ProductAdminHarness(
            WebPosDbContext context,
            IProductAdminService service,
            Guid tenantId)
        {
            Context = context;
            Service = service;
            TenantId = tenantId;
        }

        public WebPosDbContext Context { get; }

        public IProductAdminService Service { get; }

        public Guid TenantId { get; }

        public static async Task<ProductAdminHarness> CreateAsync()
        {
            Guid tenantId = TenantDefaults.MasterTenantId;
            DbContextOptions<WebPosDbContext> options =
                new DbContextOptionsBuilder<WebPosDbContext>()
                    .UseInMemoryDatabase($"WebPos_ProductAdmin_{Guid.NewGuid():N}")
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

            IDbContextFactory<WebPosDbContext> dbFactory =
                new TestDbContextFactory(options, tenantService);
            IProductAdminService service = new ProductAdminService(dbFactory, tenantService);
            return new ProductAdminHarness(context, service, tenantId);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class TestDbContextFactory(
        DbContextOptions<WebPosDbContext> options,
        ITenantService tenant) : IDbContextFactory<WebPosDbContext>
    {
        public WebPosDbContext CreateDbContext() => new(options, tenant);
    }

    private sealed class TestTenantService(Guid tenantId) : ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
