using System.Net;
using Common.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.Client.Sdk.Tests;

/// <summary>
/// Demonstrates registering <see cref="IApiClient"/> via <see cref="ServiceCollectionExtensions.AddWebPosSdk"/>
/// and resolving it from DI while swapping the primary handler for <see cref="MockHttpMessageHandler"/>.
/// </summary>
public sealed class ApiClientRegistrationTests
{
    private const string DummyBaseAddress = "https://dummy.webpos.local/";

    [Fact]
    public async Task AddWebPosSdk_Should_ResolveIApiClient_AndInjectVersionHeader()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .Expect(HttpMethod.Post, $"{DummyBaseAddress}api/sales/complete")
            .WithHeaders(ApiClient.ApiVersionHeaderName, "1.0.0")
            .Respond(HttpStatusCode.OK, "application/json", """
                {
                  "invoiceNo": "INV-DI",
                  "receiptNumber": "INV-DI",
                  "totalAmountPaisa": 15000,
                  "grossAmountPaisa": 15000,
                  "discountAmountPaisa": 0,
                  "transactionGroupId": "33333333-3333-3333-3333-333333333333"
                }
                """);

        ServiceCollection services = new();
        services.AddWebPosSdk(options =>
        {
            options.BaseAddress = DummyBaseAddress;
            options.ApiVersion = "1.0.0";
        });

        // Override the typed client's handler so no real network calls are made.
        services.AddHttpClient<IApiClient, ApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IApiClient client = provider.GetRequiredService<IApiClient>();

        CompleteSaleRequest request = new()
        {
            InvoiceNo = "INV-DI",
            ShiftId = Guid.NewGuid(),
            CashierId = Guid.NewGuid(),
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0L,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = Guid.NewGuid(),
                    BatchId = Guid.NewGuid(),
                    BatchNumber = "BATCH-DI",
                    ProductName = "DI Product",
                    Quantity = 1m,
                    UnitPricePaisa = 15_000L,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };

        // Act
        CompleteSaleResult result = await client.CompleteSaleAsync(request);

        // Assert
        result.InvoiceNo.Should().Be("INV-DI");
        result.TotalAmountPaisa.Should().Be(15_000L);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task AddWebPosSdk_ResolvedClient_Should_ThrowWebPosClientException_OnHttp500()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .When(HttpMethod.Post, $"{DummyBaseAddress}api/sales/complete")
            .Respond(HttpStatusCode.InternalServerError, "application/json", """{ "error": "boom" }""");

        ServiceCollection services = new();
        services.AddWebPosSdk(options =>
        {
            options.BaseAddress = DummyBaseAddress;
            options.ApiVersion = "1.0.0";
        });
        services.AddHttpClient<IApiClient, ApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHttp);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IApiClient client = provider.GetRequiredService<IApiClient>();

        // Act
        Func<Task> act = () => client.CompleteSaleAsync(new CompleteSaleRequest
        {
            InvoiceNo = "INV-FAIL",
            ShiftId = Guid.NewGuid(),
            CashierId = Guid.NewGuid(),
            PaymentMethod = "CASH",
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = Guid.NewGuid(),
                    BatchId = Guid.NewGuid(),
                    BatchNumber = "B1",
                    ProductName = "P1",
                    Quantity = 1m,
                    UnitPricePaisa = 100L
                }
            ]
        });

        // Assert
        await act.Should().ThrowAsync<WebPosClientException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.InternalServerError);
    }
}
