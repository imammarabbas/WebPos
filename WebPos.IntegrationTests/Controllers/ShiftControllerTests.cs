using System.Net;
using System.Net.Http.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Services;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class ShiftControllerTests
{
    [Fact]
    public async Task StartShift_ShouldCreateServerAuthorizedShift()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        (Guid cashierId, Guid terminalId) = await SeedCashierAndTerminalAsync(factory);
        StartShiftRequest body = new()
        {
            CashierId = cashierId,
            TerminalId = terminalId,
            OpeningCashPaisa = 25_000
        };

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/start",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId,
            body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDto? result = await response.Content.ReadFromJsonAsync<ShiftDto>();
        result.Should().NotBeNull();
        result!.ShiftId.Should().NotBe(Guid.Empty);
        result.CashierId.Should().Be(cashierId);
        result.TerminalId.Should().Be(terminalId);
        result.Status.Should().Be("OPEN");

        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        CashierShift persisted = await context.CashierShifts
            .IgnoreQueryFilters()
            .SingleAsync(shift => shift.Id == result.ShiftId);
        persisted.ExpectedCashPaisa.Should().Be(25_000);
    }

    [Fact]
    public async Task StartShift_ShouldReturnForbidden_WhenTerminalIsUnknown()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        (Guid cashierId, Guid terminalId) = await SeedCashierAndTerminalAsync(factory);
        Guid unknownTerminalId = Guid.NewGuid();

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/start",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId,
            new StartShiftRequest
            {
                CashierId = cashierId,
                TerminalId = unknownTerminalId,
                OpeningCashPaisa = 0
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task StartShift_ShouldAllowManagerRole()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        (Guid managerId, Guid terminalId) = await SeedOperatorAndTerminalAsync(factory, "Manager");
        StartShiftRequest body = new()
        {
            CashierId = managerId,
            TerminalId = terminalId,
            OpeningCashPaisa = 40_000
        };

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/start",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId,
            body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDto? result = await response.Content.ReadFromJsonAsync<ShiftDto>();
        result.Should().NotBeNull();
        result!.CashierId.Should().Be(managerId);
        result.Status.Should().Be("OPEN");
    }

    [Fact]
    public async Task GetOpenShift_ShouldReturnOpenShift_ThenNoContentAfterForceClose()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        (Guid cashierId, Guid terminalId) = await SeedOperatorAndTerminalAsync(factory, "Cashier");

        using HttpResponseMessage start = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/start",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId,
            new StartShiftRequest
            {
                CashierId = cashierId,
                TerminalId = terminalId,
                OpeningCashPaisa = 10_000
            });
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        ShiftDto opened = (await start.Content.ReadFromJsonAsync<ShiftDto>())!;

        using HttpResponseMessage openGet = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/shift/open?terminalId={terminalId:D}",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);
        openGet.StatusCode.Should().Be(HttpStatusCode.OK);
        OpenShiftDto? open = await openGet.Content.ReadFromJsonAsync<OpenShiftDto>();
        open.Should().NotBeNull();
        open!.ShiftId.Should().Be(opened.ShiftId);
        open.CashierId.Should().Be(cashierId);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IDbContextFactory<WebPosDbContext> dbFactory =
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<WebPosDbContext>>();
            var ambient = new AmbientDbContextAccessor();
            IShiftService shifts = new ShiftService(
                dbFactory,
                ambient,
                new TransactionService(dbFactory),
                new FixedTenantService(TestEnrollmentAuth.DefaultTenantId),
                new CashAccountService(
                    dbFactory,
                    ambient,
                    new FixedTenantService(TestEnrollmentAuth.DefaultTenantId)));
            CashVarianceReport closed = await shifts.ForceCloseShiftAsync(opened.ShiftId);
            closed.Status.Should().Be("CLOSED");
        }

        using HttpResponseMessage openAfter = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/shift/open?terminalId={terminalId:D}",
            TestEnrollmentAuth.DefaultTenantId,
            terminalId);
        openAfter.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed class FixedTenantService(Guid tenantId) : WebPos.Core.Interfaces.ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }

    private sealed class InlineTransactionService : WebPos.Core.Abstractions.ITransactionService
    {
        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            action(cancellationToken);

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);

        public Task<Guid> PostBalancedEntriesAsync(
            WebPos.Core.Abstractions.DoubleEntryPostRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task CloseShift_ShouldCloseSeededOpenShift()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/close",
            seed.TenantId,
            seed.TerminalId,
            new CloseShiftRequest
            {
                ShiftId = seed.ShiftId,
                CashierId = seed.CashierId,
                TerminalId = seed.TerminalId,
                ActualCashPaisa = 0
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteSaleThenCloseShift_ShouldSucceedOverHttp()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db);

        CompleteSaleRequest saleRequest = new()
        {
            InvoiceNo = $"INV-{Guid.NewGuid():N}",
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 2m,
                    UnitPricePaisa = seed.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        };

        using (HttpResponseMessage saleResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            saleRequest))
        {
            string saleBody = await saleResponse.Content.ReadAsStringAsync();
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK, saleBody);
        }

        using HttpResponseMessage closeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/shift/close",
            seed.TenantId,
            seed.TerminalId,
            new CloseShiftRequest
            {
                ShiftId = seed.ShiftId,
                CashierId = seed.CashierId,
                TerminalId = seed.TerminalId,
                ActualCashPaisa = 30_000
            });

        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompleteManySales_ShouldSucceedOverHttp()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db, stockQty: 500m);

        for (int i = 0; i < 25; i++)
        {
            using HttpResponseMessage saleResponse = await ApiTestClient.SendAsync(
                client,
                factory,
                HttpMethod.Post,
                "/api/sales/complete",
                seed.TenantId,
                seed.TerminalId,
                new CompleteSaleRequest
                {
                    InvoiceNo = $"INV-STRESS-{i:D3}-{Guid.NewGuid():N}"[..40],
                    ShiftId = seed.ShiftId,
                    TerminalId = seed.TerminalId,
                    CashierId = seed.CashierId,
                    PaymentMethod = "CASH",
                    DiscountAmountPaisa = i % 5 == 0 ? 100 : 0,
                    Lines =
                    [
                        new SaleLineRequest
                        {
                            ProductId = seed.ProductId,
                            BatchId = seed.BatchId,
                            BatchNumber = seed.BatchNumber,
                            ProductName = seed.ProductName,
                            Quantity = 1m + (i % 3),
                            UnitPricePaisa = seed.UnitPricePaisa,
                            DiscountAppliedPaisa = 0
                        }
                    ]
                });

            string body = await saleResponse.Content.ReadAsStringAsync();
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"sale #{i}: {body}");
        }
    }

    [Fact]
    public async Task CompleteAliasPackSale_ManyTimes_ShouldDeductParentStock()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db, stockQty: 100m);

        Guid parentId = Guid.NewGuid();
        Guid packId = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.Products.Add(new Product
        {
            Id = parentId,
            TenantId = seed.TenantId,
            Name = "Bulk Chana 20kg",
            Sku = $"SKU-P-{parentId:N}"[..20],
            Barcode = $"BC-P-{parentId:N}"[..20],
            ShortCode = $"{Random.Shared.Next(5000, 9999)}",
            Brand = "Test",
            BaseUnit = "kg",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            IsBulk = true,
            StockQty = 20m,
            CostPricePaisa = 10_000L,
            RetailPricePaisa = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Products.Add(new Product
        {
            Id = packId,
            TenantId = seed.TenantId,
            Name = "Chana 1kg",
            Sku = $"SKU-C-{packId:N}"[..20],
            Barcode = $"BC-C-{packId:N}"[..20],
            ShortCode = "c200",
            Brand = "Test",
            BaseUnit = "kg",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            IsBulk = false,
            ParentProductId = parentId,
            DeductionMultiplier = 1m,
            StockQty = 0,
            CostPricePaisa = 10_000L,
            RetailPricePaisa = 30_000L,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        for (int i = 0; i < 10; i++)
        {
            using HttpResponseMessage saleResponse = await ApiTestClient.SendAsync(
                client,
                factory,
                HttpMethod.Post,
                "/api/sales/complete",
                seed.TenantId,
                seed.TerminalId,
                new CompleteSaleRequest
                {
                    InvoiceNo = $"INV-PACK-{i:D2}-{Guid.NewGuid():N}"[..40],
                    ShiftId = seed.ShiftId,
                    TerminalId = seed.TerminalId,
                    CashierId = seed.CashierId,
                    PaymentMethod = "CASH",
                    DiscountAmountPaisa = 0,
                    Lines =
                    [
                        new SaleLineRequest
                        {
                            ProductId = packId,
                            BatchId = null,
                            BatchNumber = string.Empty,
                            ProductName = "Chana 1kg",
                            Quantity = 1m,
                            UnitPricePaisa = 30_000L,
                            DiscountAppliedPaisa = 0
                        }
                    ]
                });

            string body = await saleResponse.Content.ReadAsStringAsync();
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK, $"pack sale #{i}: {body}");
        }

        db.ChangeTracker.Clear();
        Product parent = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(p => p.Id == parentId);
        parent.StockQty.Should().Be(10m);
    }

    [Fact]
    public async Task CompleteSaleThenSecondSale_ShouldSucceedOverHttp()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db);

        for (int i = 0; i < 2; i++)
        {
            using HttpResponseMessage saleResponse = await ApiTestClient.SendAsync(
                client,
                factory,
                HttpMethod.Post,
                "/api/sales/complete",
                seed.TenantId,
                seed.TerminalId,
                new CompleteSaleRequest
                {
                    InvoiceNo = $"INV-{Guid.NewGuid():N}",
                    ShiftId = seed.ShiftId,
                    TerminalId = seed.TerminalId,
                    CashierId = seed.CashierId,
                    PaymentMethod = "CASH",
                    DiscountAmountPaisa = 0,
                    Lines =
                    [
                        new SaleLineRequest
                        {
                            ProductId = seed.ProductId,
                            BatchId = seed.BatchId,
                            BatchNumber = seed.BatchNumber,
                            ProductName = seed.ProductName,
                            Quantity = 1m,
                            UnitPricePaisa = seed.UnitPricePaisa,
                            DiscountAppliedPaisa = 0
                        }
                    ]
                });

            string body = await saleResponse.Content.ReadAsStringAsync();
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK, body);
        }
    }

    [Fact]
    public async Task TwoSequentialProductGets_ShouldSucceedOverHttp()
    {
        await using SalesApiFactory factory = new();
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        SaleSeedData seed = await SeedHelper.SeedSalePrerequisitesAsync(db);

        for (int i = 0; i < 2; i++)
        {
            HttpClient client = factory.CreateClient();
            using HttpResponseMessage response = await ApiTestClient.SendAsync(
                client,
                factory,
                HttpMethod.Get,
                "/api/products/for-sale",
                seed.TenantId,
                seed.TerminalId);

            string body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        }
    }

    private static Task<(Guid CashierId, Guid TerminalId)> SeedCashierAndTerminalAsync(
        SalesApiFactory factory) =>
        SeedOperatorAndTerminalAsync(factory, "Cashier");

    private static async Task<(Guid CashierId, Guid TerminalId)> SeedOperatorAndTerminalAsync(
        SalesApiFactory factory,
        string roleName)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext context = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        Guid tenantId = TenantDefaults.MasterTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Role? existingRole = await context.Roles.FirstOrDefaultAsync(
            role => role.RoleName == roleName && role.TenantId == tenantId);
        Guid roleId = existingRole?.Id ?? Guid.NewGuid();
        if (existingRole is null)
        {
            context.Roles.Add(new Role
            {
                Id = roleId,
                TenantId = tenantId,
                RoleName = roleName,
                CreatedAt = now
            });
        }

        User operatorUser = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Username = $"shift-{roleName.ToLowerInvariant()}-{Guid.NewGuid():N}",
            PasswordHash = "test-hash",
            RoleId = roleId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        Terminal terminal = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TerminalName = $"Shift Terminal {Guid.NewGuid():N}",
            MacAddress = Guid.NewGuid().ToString("N"),
            IsActive = true,
            LastSyncTime = now
        };

        context.Users.Add(operatorUser);
        context.Terminals.Add(terminal);
        await context.SaveChangesAsync();
        return (operatorUser.Id, terminal.Id);
    }
}
