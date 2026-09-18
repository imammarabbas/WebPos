using System.Net;
using System.Net.Http.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;
using WebPos.IntegrationTests.Infrastructure;
using ApiCompleteSaleRequest = Common.Models.CompleteSaleRequest;
using ApiCompleteSaleResult = Common.Models.CompleteSaleResult;
using ApiSaleLineRequest = Common.Models.SaleLineRequest;

namespace WebPos.IntegrationTests.E2E;

/// <summary>
/// MVP pilot script: HTTP sale plus shift close reconciliation against the ledger.
/// </summary>
public sealed class PilotFlowTests
{
    [Fact]
    public async Task PilotFlow_EnrollShiftSaleClose_ShouldReconcileCashLedger()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        await factory.EnsureTenantAsync();
        SaleSeedData seed = await SeedCatalogAsync(factory);

        string invoiceNo = $"PILOT-{Guid.NewGuid():N}";
        ApiCompleteSaleRequest saleRequest = new()
        {
            InvoiceNo = invoiceNo,
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new ApiSaleLineRequest
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
            ApiCompleteSaleResult? saleResult =
                await saleResponse.Content.ReadFromJsonAsync<ApiCompleteSaleResult>();
            saleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            saleResult.Should().NotBeNull();
            saleResult!.TotalAmountPaisa.Should().Be(30_000);
        }

        CashVarianceReport report;
        using (HttpResponseMessage closeResponse = await ApiTestClient.SendAsync(
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
            }))
        {
            closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            report = (await closeResponse.Content.ReadFromJsonAsync<CashVarianceReport>())!;
        }

        report.ExpectedCashPaisa.Should().Be(30_000);
        report.DiscrepancyPaisa.Should().Be(0);

        using IServiceScope scope = factory.Services.CreateScope();
        DbContextOptions<WebPosDbContext> options =
            scope.ServiceProvider.GetRequiredService<DbContextOptions<WebPosDbContext>>();
        await using WebPosDbContext db = new(options, new FixedTenantService(seed.TenantId));

        long cashDebits = await db.GeneralLedgerEntries
            .IgnoreQueryFilters()
            .Where(e =>
                e.TenantId == seed.TenantId
                && (e.AccountCode == LedgerAccounts.Cash
                    || e.AccountCode.StartsWith("CASH:TILL:")))
            .SumAsync(e => e.DebitPaisa);

        long cashCredits = await db.GeneralLedgerEntries
            .IgnoreQueryFilters()
            .Where(e =>
                e.TenantId == seed.TenantId
                && (e.AccountCode == LedgerAccounts.Cash
                    || e.AccountCode.StartsWith("CASH:TILL:")))
            .SumAsync(e => e.CreditPaisa);

        (cashDebits - cashCredits).Should().BeGreaterThanOrEqualTo(30_000);

        Product product = await db.Products
            .IgnoreQueryFilters()
            .SingleAsync(p => p.Id == seed.ProductId);
        product.StockQty.Should().Be(seed.InitialQty - 2m);

        CashierShift closedShift = await db.CashierShifts
            .IgnoreQueryFilters()
            .SingleAsync(s => s.Id == seed.ShiftId);
        closedShift.Status.Should().Be("CLOSED");
    }

    private static async Task<SaleSeedData> SeedCatalogAsync(SalesApiFactory factory)
    {
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        return await SeedHelper.SeedSalePrerequisitesAsync(db);
    }

    private sealed class FixedTenantService(Guid tenantId) : WebPos.Core.Interfaces.ITenantService
    {
        public Guid TenantId { get; } = tenantId;

        public bool IsResolved => TenantId != Guid.Empty;
    }
}
