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
    public async Task GetProductByBarcode_Should_ReturnSellableBatch()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/products/by-barcode/{Uri.EscapeDataString(seed.Barcode)}",
            seed.TenantId,
            seed.TerminalId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SalesProductDto? product =
            await ApiTestClient.ReadJsonAsync<SalesProductDto>(response, JsonOptions);

        product.Should().NotBeNull();
        product!.ProductId.Should().Be(seed.ProductId);
        product.Barcode.Should().Be(seed.Barcode);
        product.UnitPricePaisa.Should().Be(seed.UnitPricePaisa);
        product.AvailableStock.Should().Be(10m);
    }

    [Fact]
    public async Task GetProductByBarcode_Should_Return404_WhenUnknown()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);

        using HttpResponseMessage response = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/products/by-barcode/UNKNOWN-BARCODE-XYZ",
            seed.TenantId,
            seed.TerminalId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListInvoices_Should_ReturnCompletedSaleForShift()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage completeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage listResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/sales/invoices?shiftId={seed.ShiftId:D}&limit=10",
            seed.TenantId,
            seed.TerminalId);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesInvoiceSummaryDto>? invoices =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(listResponse, JsonOptions);

        SalesInvoiceSummaryDto invoice = invoices.Should().ContainSingle(
            item => item.InvoiceNo == request.InvoiceNo).Subject;
        invoice.TotalAmountPaisa.Should().Be(30_000L);
        invoice.PaymentMethod.Should().Be("CASH");
        invoice.ItemCount.Should().Be(1);
        invoice.ReturnedAmountPaisa.Should().Be(0L);
        invoice.ProductLabels.Should().Contain(seed.ShortCode);
    }

    [Fact]
    public async Task ListInvoices_WithoutShiftId_Should_ReturnTenantWideRecent()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage completeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage listResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/sales/invoices?limit=20",
            seed.TenantId,
            seed.TerminalId);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesInvoiceSummaryDto>? invoices =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(listResponse, JsonOptions);

        SalesInvoiceSummaryDto invoice = invoices.Should().Contain(
            item => item.InvoiceNo == request.InvoiceNo).Subject;
        invoice.TerminalId.Should().Be(seed.TerminalId);
    }

    [Fact]
    public async Task GetInvoice_Should_ReturnLinesWithReturnableQuantity()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage completeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage detailResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/sales/invoices/{Uri.EscapeDataString(request.InvoiceNo)}",
            seed.TenantId,
            seed.TerminalId);

        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        SalesInvoiceDetailDto? detail =
            await ApiTestClient.ReadJsonAsync<SalesInvoiceDetailDto>(detailResponse, JsonOptions);

        detail.Should().NotBeNull();
        detail!.InvoiceNo.Should().Be(request.InvoiceNo);
        detail.ShiftId.Should().Be(seed.ShiftId);
        SalesInvoiceLineDto line = detail.Lines.Should().ContainSingle().Subject;
        line.ProductId.Should().Be(seed.ProductId);
        line.QuantitySold.Should().Be(2m);
        line.QuantityReturned.Should().Be(0m);
        line.ReturnableQuantity.Should().Be(2m);
    }

    [Fact]
    public async Task SearchInvoices_Should_MatchInvoiceFragmentAndProductLabels()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage completeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        string fragment = request.InvoiceNo[^8..];
        using HttpResponseMessage searchResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/sales/invoices/search?invoice={Uri.EscapeDataString(fragment)}&limit=20",
            seed.TenantId,
            seed.TerminalId);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesInvoiceSummaryDto>? invoices =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(searchResponse, JsonOptions);

        SalesInvoiceSummaryDto invoice = invoices.Should().ContainSingle(
            item => item.InvoiceNo == request.InvoiceNo).Subject;
        invoice.ProductLabels.Should().Contain(seed.ShortCode);
        invoice.ItemCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchInvoices_Should_MatchProductShortCode()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpResponseMessage completeResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Post,
            "/api/sales/complete",
            seed.TenantId,
            seed.TerminalId,
            request);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using HttpResponseMessage searchResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            $"/api/sales/invoices/search?product={Uri.EscapeDataString(seed.ShortCode)}&limit=20",
            seed.TenantId,
            seed.TerminalId);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        IReadOnlyList<SalesInvoiceSummaryDto>? invoices =
            await ApiTestClient.ReadJsonAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(searchResponse, JsonOptions);

        invoices.Should().Contain(item => item.InvoiceNo == request.InvoiceNo);
    }

    [Fact]
    public async Task SearchInvoices_Should_Return400_WhenNoCriteriaProvided()
    {
        await using SalesApiFactory factory = new();
        HttpClient client = factory.CreateClient();
        SaleSeedData seed = await SeedAsync(factory);

        using HttpResponseMessage searchResponse = await ApiTestClient.SendAsync(
            client,
            factory,
            HttpMethod.Get,
            "/api/sales/invoices/search",
            seed.TenantId,
            seed.TerminalId);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
