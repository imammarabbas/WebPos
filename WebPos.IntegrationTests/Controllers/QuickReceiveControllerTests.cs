using System.Net;
using System.Text.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class QuickReceiveControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task QuickReceive_ShouldAddStock_WhenPricesMatchLastBatch()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            seed = await SeedHelper.SeedSalePrerequisitesAsync(db);
        }

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/quick-receive",
            seed.TenantId,
            seed.TerminalId,
            new QuickReceiveRequestDto
            {
                SupplierId = seed.SupplierId,
                ReceiverId = seed.CashierId,
                SupplierInvoiceNo = $"QR-{Guid.NewGuid():N}"[..20],
                PurchaseDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new QuickReceiveLineDto
                    {
                        ProductId = seed.ProductId,
                        Quantity = 5m,
                        PurchasePricePaisa = 10_000L,
                        RetailPricePaisa = SeedHelper.RetailPricePaisa,
                        BatchNumber = "QR-BATCH-1"
                    }
                ]
            });

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        ReceiveStockResultDto? result =
            await ApiTestClient.ReadJsonAsync<ReceiveStockResultDto>(response, JsonOptions);
        result.Should().NotBeNull();
        result!.BatchIds.Should().HaveCount(1);

        using HttpResponseMessage forSale = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/products/for-sale",
            seed.TenantId,
            seed.TerminalId);
        forSale.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesProductDto>? products =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesProductDto>>(forSale, JsonOptions);
        products.Should().Contain(p =>
            p.ProductId == seed.ProductId && p.BatchNumber == "QR-BATCH-1" && p.AvailableStock == 5m);
    }

    [Fact]
    public async Task ForReceive_ShouldReturnZeroStockProducts()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed;
        Guid orphanId = Guid.NewGuid();
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            seed = await SeedHelper.SeedSalePrerequisitesAsync(db);

            ProductBatch batch = await db.ProductBatches
                .IgnoreQueryFilters()
                .SingleAsync(b => b.Id == seed.BatchId);
            batch.CurrentQty = 0m;

            DateTimeOffset now = DateTimeOffset.UtcNow;
            db.Products.Add(new Product
            {
                Id = orphanId,
                TenantId = seed.TenantId,
                Name = "Orphan Zero Stock",
                Sku = $"SKU-{orphanId:N}"[..20],
                Barcode = $"BC-{orphanId:N}"[..20],
                ShortCode = "ORZ",
                Brand = "Test",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                ShowOnWebshop = false,
                MinStockQty = 1m,
                CreatedAt = now,
                UpdatedAt = now
            });
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage forSale = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/products/for-sale",
            seed.TenantId,
            seed.TerminalId);
        forSale.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesProductDto>? saleProducts =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesProductDto>>(forSale, JsonOptions);
        saleProducts.Should().NotContain(p => p.ProductId == seed.ProductId);
        saleProducts.Should().NotContain(p => p.ProductId == orphanId);

        using HttpResponseMessage forReceive = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/products/for-receive",
            seed.TenantId,
            seed.TerminalId);
        forReceive.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesProductDto>? receiveProducts =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesProductDto>>(forReceive, JsonOptions);
        receiveProducts.Should().Contain(p => p.ProductId == seed.ProductId && p.AvailableStock == 0m);
        receiveProducts.Should().Contain(p => p.ProductId == orphanId && p.AvailableStock == 0m);
    }

    [Fact]
    public async Task QuickReceive_ShouldRequireManagerPin_WhenRetailChanges()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            seed = await SeedHelper.SeedSalePrerequisitesAsync(db);
        }

        const string managerPin = "8642";
        await SeedManagerAsync(factory, managerPin);

        QuickReceiveRequestDto request = new()
        {
            SupplierId = seed.SupplierId,
            ReceiverId = seed.CashierId,
            SupplierInvoiceNo = $"QR-{Guid.NewGuid():N}"[..20],
            PurchaseDate = DateTimeOffset.UtcNow,
            Lines =
            [
                new QuickReceiveLineDto
                {
                    ProductId = seed.ProductId,
                    Quantity = 2m,
                    PurchasePricePaisa = 10_000L,
                    RetailPricePaisa = SeedHelper.RetailPricePaisa + 500,
                    BatchNumber = "QR-PRICE"
                }
            ]
        };

        using HttpResponseMessage denied = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/quick-receive",
            seed.TenantId,
            seed.TerminalId,
            request);
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using HttpResponseMessage allowed = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/quick-receive",
            seed.TenantId,
            seed.TerminalId,
            new QuickReceiveRequestDto
            {
                SupplierId = request.SupplierId,
                ReceiverId = request.ReceiverId,
                SupplierInvoiceNo = request.SupplierInvoiceNo,
                PurchaseDate = request.PurchaseDate,
                ManagerPin = managerPin,
                Lines = request.Lines
            });

        string body = await allowed.Content.ReadAsStringAsync();
        allowed.StatusCode.Should().Be(HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task QuickReceive_ShouldRequireManagerPin_ForNewProduct()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            seed = await SeedHelper.SeedSalePrerequisitesAsync(db);
        }

        const string managerPin = "9753";
        await SeedManagerAsync(factory, managerPin, "Owner");

        using HttpResponseMessage denied = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/quick-receive",
            seed.TenantId,
            seed.TerminalId,
            new QuickReceiveRequestDto
            {
                SupplierId = seed.SupplierId,
                ReceiverId = seed.CashierId,
                SupplierInvoiceNo = $"QR-{Guid.NewGuid():N}"[..20],
                PurchaseDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new QuickReceiveLineDto
                    {
                        NewProduct = new QuickReceiveNewProductDto
                        {
                            Name = "Brand New Milk",
                            ShortCode = "NEWM"
                        },
                        Quantity = 3m,
                        PurchasePricePaisa = 8_000L,
                        RetailPricePaisa = 12_000L
                    }
                ]
            });
        denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using HttpResponseMessage allowed = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/quick-receive",
            seed.TenantId,
            seed.TerminalId,
            new QuickReceiveRequestDto
            {
                SupplierId = seed.SupplierId,
                ReceiverId = seed.CashierId,
                SupplierInvoiceNo = $"QR-{Guid.NewGuid():N}"[..20],
                PurchaseDate = DateTimeOffset.UtcNow,
                ManagerPin = managerPin,
                Lines =
                [
                    new QuickReceiveLineDto
                    {
                        NewProduct = new QuickReceiveNewProductDto
                        {
                            Name = "Brand New Milk",
                            ShortCode = "NEWM"
                        },
                        Quantity = 3m,
                        PurchasePricePaisa = 8_000L,
                        RetailPricePaisa = 12_000L
                    }
                ]
            });

        string body = await allowed.Content.ReadAsStringAsync();
        allowed.StatusCode.Should().Be(HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task QuickReceive_ShouldCreateSinglePurchaseOrder_ForMultipleLines()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed;
        Guid secondProductId;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            seed = await SeedHelper.SeedSalePrerequisitesAsync(db);
            secondProductId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            db.Products.Add(new Product
            {
                Id = secondProductId,
                TenantId = seed.TenantId,
                Name = "Second QR Product",
                Sku = $"SKU-{Guid.NewGuid():N}"[..12],
                Barcode = $"BC-{Guid.NewGuid():N}"[..12],
                ShortCode = "QR2",
                Brand = "Test",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                ShowOnWebshop = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            db.ProductBatches.Add(new ProductBatch
            {
                Id = Guid.NewGuid(),
                TenantId = seed.TenantId,
                ProductId = secondProductId,
                BatchNumber = "OPEN-2",
                CostPricePaisa = 10_000L,
                RetailPricePaisa = SeedHelper.RetailPricePaisa,
                InitialQty = 10m,
                CurrentQty = 10m,
                SupplierId = seed.SupplierId,
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        }

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/purchases/direct-receive",
            seed.TenantId,
            seed.TerminalId,
            new QuickReceiveRequestDto
            {
                SupplierId = seed.SupplierId,
                ReceiverId = seed.CashierId,
                SupplierInvoiceNo = $"QR-MULTI-{Guid.NewGuid():N}"[..20],
                PurchaseDate = DateTimeOffset.UtcNow,
                Lines =
                [
                    new QuickReceiveLineDto
                    {
                        ProductId = seed.ProductId,
                        Quantity = 2m,
                        PurchasePricePaisa = 10_000L,
                        RetailPricePaisa = SeedHelper.RetailPricePaisa,
                        BatchNumber = "BATCH-A"
                    },
                    new QuickReceiveLineDto
                    {
                        ProductId = secondProductId,
                        Quantity = 3m,
                        PurchasePricePaisa = 10_000L,
                        RetailPricePaisa = SeedHelper.RetailPricePaisa,
                        BatchNumber = "BATCH-A"
                    }
                ]
            });

        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        ReceiveStockResultDto? result =
            await ApiTestClient.ReadJsonAsync<ReceiveStockResultDto>(response, JsonOptions);
        result.Should().NotBeNull();
        result!.BatchIds.Should().HaveCount(2);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
            int orderCount = await db.PurchaseOrders
                .IgnoreQueryFilters()
                .CountAsync(o => o.Id == result.PurchaseOrderId);
            orderCount.Should().Be(1);
            int lineCount = await db.PurchaseItems
                .IgnoreQueryFilters()
                .CountAsync(i => i.PurchaseOrderId == result.PurchaseOrderId);
            lineCount.Should().Be(2);
        }
    }

    private static async Task SeedManagerAsync(
        SalesApiFactory factory,
        string pin,
        string roleName = "Manager")
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        IPinHasher pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();
        Guid tenantId = TestEnrollmentAuth.DefaultTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Role? existing = await context.Roles.FirstOrDefaultAsync(
            r => r.RoleName == roleName && r.TenantId == tenantId);
        Guid roleId = existing?.Id ?? Guid.NewGuid();
        if (existing is null)
        {
            context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = roleName,
                CreatedAt = now
            });
        }

        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}",
            PasswordHash = "x",
            PinHash = pinHasher.HashPin(pin),
            RoleId = roleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }
}
