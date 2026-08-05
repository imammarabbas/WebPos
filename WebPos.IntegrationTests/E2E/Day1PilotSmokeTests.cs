using System.Net;
using System.Net.Http.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Security;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.E2E;

/// <summary>
/// Full day-1 pilot: enroll → PIN login → start shift → sale → close → ledger reconcile.
/// </summary>
public sealed class Day1PilotSmokeTests
{
    private const string AdminPassword = "admin123";
    private const string CashierPin = "2468";

    [Fact]
    public async Task Day1_EnrollLoginShiftSaleClose_ShouldReconcile()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        Day1Seed seed = await SeedAsync(factory);

        // 1) Enroll (anonymous)
        EnrollmentCertificateDto enroll;
        using (HttpResponseMessage enrollResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/terminal-enrollment",
            new EnrollTerminalRequest
            {
                TerminalId = seed.TerminalId,
                AdminUsername = "admin",
                AdminPassword = AdminPassword
            },
            bearerToken: null))
        {
            enrollResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            enroll = (await enrollResponse.Content.ReadFromJsonAsync<EnrollmentCertificateDto>())!;
            enroll.Token.Should().NotBeNullOrWhiteSpace();
        }

        string bearer = enroll.Token;

        // 2) PIN login
        CashierDto cashier;
        using (HttpResponseMessage loginResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/auth/login",
            new LoginRequest { Pin = CashierPin },
            bearerToken: bearer))
        {
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            cashier = (await loginResponse.Content.ReadFromJsonAsync<CashierDto>())!;
            cashier.CashierId.Should().Be(seed.CashierId);
        }

        // 3) Start shift
        ShiftDto shift;
        using (HttpResponseMessage startResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/shift/start",
            new StartShiftRequest
            {
                CashierId = cashier.CashierId,
                TerminalId = seed.TerminalId,
                OpeningCashPaisa = 0
            },
            bearerToken: bearer))
        {
            startResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            shift = (await startResponse.Content.ReadFromJsonAsync<ShiftDto>())!;
            shift.Status.Should().Be("OPEN");
        }

        // 4) Cash sale
        long expectedTotal = seed.UnitPricePaisa * 2;
        CompleteSaleResult sale;
        using (HttpResponseMessage saleResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/sales/complete",
            new CompleteSaleRequest
            {
                InvoiceNo = $"DAY1-{Guid.NewGuid():N}",
                ShiftId = shift.ShiftId,
                TerminalId = seed.TerminalId,
                CashierId = cashier.CashierId,
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
            },
            bearerToken: bearer))
        {
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            sale = (await saleResponse.Content.ReadFromJsonAsync<CompleteSaleResult>())!;
            sale.TotalAmountPaisa.Should().Be(expectedTotal);
        }

        // 5) Close shift
        CashVarianceReport report;
        using (HttpResponseMessage closeResponse = await ApiTestClient.SendWithBearerAsync(
            client,
            HttpMethod.Post,
            "/api/shift/close",
            new CloseShiftRequest
            {
                ShiftId = shift.ShiftId,
                CashierId = cashier.CashierId,
                TerminalId = seed.TerminalId,
                ActualCashPaisa = expectedTotal
            },
            bearerToken: bearer))
        {
            closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            report = (await closeResponse.Content.ReadFromJsonAsync<CashVarianceReport>())!;
        }

        report.ExpectedCashPaisa.Should().Be(expectedTotal);
        report.DiscrepancyPaisa.Should().Be(0);
        report.IsBalanced.Should().BeTrue();

        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();

        long cashNet = await db.GeneralLedgerEntries
            .IgnoreQueryFilters()
            .Where(e =>
                e.TenantId == seed.TenantId
                && e.AccountCode == LedgerAccounts.Cash)
            .SumAsync(e => e.DebitPaisa - e.CreditPaisa);
        cashNet.Should().BeGreaterThanOrEqualTo(expectedTotal);

        ProductBatch batch = await db.ProductBatches
            .IgnoreQueryFilters()
            .SingleAsync(b => b.Id == seed.BatchId);
        batch.CurrentQty.Should().Be(seed.InitialQty - 2m);

        CashierShift closed = await db.CashierShifts
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == shift.ShiftId);
        closed.Status.Should().Be("CLOSED");
    }

    private static async Task<Day1Seed> SeedAsync(SalesApiFactory factory)
    {
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        IPinHasher pinHasher = scope.ServiceProvider.GetRequiredService<IPinHasher>();

        Guid tenantId = TenantDefaults.MasterTenantId;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid adminRoleId = Guid.NewGuid();
        Guid cashierRoleId = Guid.NewGuid();
        Guid cashierId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        Guid supplierId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        Guid batchId = Guid.NewGuid();
        string batchNumber = $"DAY1-{batchId:N}"[..20];
        const string productName = "Buffalo Milk";
        const long unitPrice = 22_000L;
        const decimal initialQty = 50m;

        db.Roles.AddRange(
            new Role
            {
                Id = adminRoleId,
                TenantId = tenantId,
                RoleName = "Admin",
                CreatedAt = now
            },
            new Role
            {
                Id = cashierRoleId,
                TenantId = tenantId,
                RoleName = "Cashier",
                CreatedAt = now
            });

        db.Users.AddRange(
            new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Username = "admin",
                PasswordHash = CryptoHelper.HashPassword(AdminPassword),
                PinHash = string.Empty,
                RoleId = adminRoleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            },
            new User
            {
                Id = cashierId,
                TenantId = tenantId,
                Username = "cashier",
                PasswordHash = CryptoHelper.HashPassword(CashierPin),
                PinHash = pinHasher.HashPin(CashierPin),
                RoleId = cashierRoleId,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            });

        db.Terminals.Add(new Terminal
        {
            Id = terminalId,
            TenantId = tenantId,
            TerminalName = "Day1 Terminal",
            MacAddress = Guid.NewGuid().ToString("N")[..20],
            IsActive = true,
            LastSyncTime = now
        });

        db.Parties.Add(new Party
        {
            Id = supplierId,
            TenantId = tenantId,
            PartyType = "SUPPLIER",
            Name = "Day1 Farm",
            PhoneNumber = "03009998877",
            Address = "Farm",
            CreditLimitPaisa = 0,
            CurrentBalancePaisa = 0,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = productName,
            Sku = $"SKU-{productId:N}"[..20],
            Barcode = $"BC-{productId:N}"[..20],
            Brand = "Pilot",
            BaseUnit = "L",
            ConversionMultiplier = 1,
            ShowOnWebshop = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.ProductBatches.Add(new ProductBatch
        {
            Id = batchId,
            TenantId = tenantId,
            ProductId = productId,
            BatchNumber = batchNumber,
            ExpiryDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(2)),
            CostPricePaisa = 18_000,
            RetailPricePaisa = unitPrice,
            InitialQty = initialQty,
            CurrentQty = initialQty,
            SupplierId = supplierId,
            RackLocation = "COLD-1",
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        return new Day1Seed(
            tenantId,
            cashierId,
            terminalId,
            productId,
            productName,
            batchId,
            batchNumber,
            initialQty,
            unitPrice);
    }

    private sealed record Day1Seed(
        Guid TenantId,
        Guid CashierId,
        Guid TerminalId,
        Guid ProductId,
        string ProductName,
        Guid BatchId,
        string BatchNumber,
        decimal InitialQty,
        long UnitPricePaisa);
}
