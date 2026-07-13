using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Filters;
using WebPos.IntegrationTests.Infrastructure;

namespace WebPos.IntegrationTests.Controllers;

public sealed class SalesControllerTests : IClassFixture<SalesApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SalesApiFactory _factory;
    private readonly HttpClient _client;

    public SalesControllerTests(SalesApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteSale_Should_Return200_WhenRequestIsValid()
    {
        SaleSeedData seed = await SeedAsync();
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/sales/complete");
        httpRequest.Headers.Add(ApiVersionFilter.HeaderName, "1.0.0");
        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        CompleteSaleResult? body = await response.Content.ReadFromJsonAsync<CompleteSaleResult>(JsonOptions);
        body.Should().NotBeNull();
        body!.InvoiceNo.Should().Be(request.InvoiceNo);
        body.GrossAmountPaisa.Should().Be(30_000L);
        body.TotalAmountPaisa.Should().Be(30_000L);
        body.TransactionGroupId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CompleteSale_Should_Return426_WhenVersionHeaderIsMissing()
    {
        SaleSeedData seed = await SeedAsync();
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 0L);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/sales/complete");
        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be((HttpStatusCode)426);
    }

    [Fact]
    public async Task CompleteSale_Should_Return400_WhenDiscountExceedsGrossAmount()
    {
        SaleSeedData seed = await SeedAsync();
        // Gross = 2 * 15000 = 30000; discount exceeds gross.
        CompleteSaleRequest request = BuildValidRequest(seed, discountAmountPaisa: 50_000L);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/sales/complete");
        httpRequest.Headers.Add(ApiVersionFilter.HeaderName, "1.0.0");
        httpRequest.Content = JsonContent.Create(request);

        HttpResponseMessage response = await _client.SendAsync(httpRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Discount cannot exceed the gross sale amount.");
    }

    private async Task<SaleSeedData> SeedAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
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
