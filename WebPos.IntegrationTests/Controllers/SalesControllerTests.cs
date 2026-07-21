using System.Net;
using System.Text.Json;
using Common.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Data;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class SalesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task CompleteSale_Should_Return200_WhenRequestIsValid()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CompleteSaleResult? body = await ApiTestClient.ReadJsonAsync<CompleteSaleResult>(response, JsonOptions);
        body.Should().NotBeNull();
        body!.InvoiceNo.Should().Be(request.InvoiceNo);
        body.GrossAmountPaisa.Should().Be(30_000L);
        body.TotalAmountPaisa.Should().Be(30_000L);
        body.TransactionGroupId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CompleteSale_Should_Return426_WhenVersionHeaderIsMissing()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request,
            includeVersionHeader: false);

        response.StatusCode.Should().Be((HttpStatusCode)426);
    }

    [Fact]
    public async Task CompleteSale_Should_Return403_WhenShiftDoesNotMatchTerminal()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest validRequest = BuildValidRequest(seed, discountAmountPaisa: 0L);
        CompleteSaleRequest request = new()
        {
            InvoiceNo = validRequest.InvoiceNo,
            ShiftId = validRequest.ShiftId,
            TerminalId = Guid.NewGuid(),
            CashierId = validRequest.CashierId,
            PaymentMethod = validRequest.PaymentMethod,
            DiscountAmountPaisa = validRequest.DiscountAmountPaisa,
            Lines = validRequest.Lines
        };

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteSale_Should_Return401_WhenEnrollmentTokenMissing()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request,
            includeEnrollmentAuth: false);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompleteSale_Should_Return400_WhenDiscountExceedsGrossAmount()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 50_000L);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Discount cannot exceed the gross sale amount.");
    }

    [Fact]
    public async Task GetProductsForSale_Should_ReturnCurrentBatchPriceAndStock()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/products/for-sale",
            seed.TenantId,
            seed.TerminalId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesProductDto>? products =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesProductDto>>(response, JsonOptions);

        SalesProductDto product = products.Should().ContainSingle(
            item => item.BatchId == seed.BatchId).Subject;
        product.ProductId.Should().Be(seed.ProductId);
        product.UnitPricePaisa.Should().Be(seed.UnitPricePaisa);
        product.AvailableStock.Should().Be(10m);
    }

    [Fact]
    public async Task CompleteSale_Should_Return400_WhenClientPriceIsStale()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);
        request = new CompleteSaleRequest
        {
            InvoiceNo = request.InvoiceNo,
            ShiftId = request.ShiftId,
            TerminalId = request.TerminalId,
            CashierId = request.CashierId,
            PaymentMethod = request.PaymentMethod,
            DiscountAmountPaisa = request.DiscountAmountPaisa,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = seed.ProductId,
                    BatchId = seed.BatchId,
                    BatchNumber = seed.BatchNumber,
                    ProductName = seed.ProductName,
                    Quantity = 1m,
                    UnitPricePaisa = seed.UnitPricePaisa + 1,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("price");
    }

    private static async Task<SaleSeedData> SeedAsync(SalesApiFactory factory)
    {
        await factory.EnsureTenantAsync();
        using IServiceScope scope = factory.Services.CreateScope();
        WebPosDbContext db = scope.ServiceProvider.GetRequiredService<WebPosDbContext>();
        return await SeedHelper.SeedSalePrerequisitesAsync(db);
    }

    private static CompleteSaleRequest BuildValidRequest(SaleSeedData seed, long discountAmountPaisa) =>
        new()
        {
            InvoiceNo = $"INV-{Guid.NewGuid():N}",
            ShiftId = seed.ShiftId,
            TerminalId = seed.TerminalId,
            CashierId = seed.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = discountAmountPaisa,
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
                    DiscountAppliedPaisa = 0L
                }
            ]
        };
}
