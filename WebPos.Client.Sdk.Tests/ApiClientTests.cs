using System.Net;
using System.Text;
using System.Text.Json;
using Common.Models;
using FluentAssertions;
using Moq;
using Moq.Protected;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.Client.Sdk.Tests;

public sealed class ApiClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly ApiClient _apiClient;

    public ApiClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost/")
        };
        _apiClient = new ApiClient(_httpClient);
    }

    [Fact]
    public async Task CompleteSaleAsync_ShouldIncludeApiVersionHeader()
    {
        // Arrange
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Headers.Contains("X-Api-Version")
                    && req.Headers.GetValues("X-Api-Version").Contains("1.0.0")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(CreateValidSaleResultJson("INV-HEADER"), Encoding.UTF8, "application/json")
            });

        // Act
        await _apiClient.CompleteSaleAsync(CreateValidSaleRequest("INV-HEADER"));

        // Assert
        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Contains("X-Api-Version")
                && req.Headers.GetValues("X-Api-Version").Contains("1.0.0")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_ShouldPostPinAndReturnCashier()
    {
        // Arrange
        Guid cashierId = Guid.NewGuid();
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post
                    && request.RequestUri == new Uri("http://localhost/api/auth/login")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    $$"""{"cashierId":"{{cashierId}}","name":"Cashier"}""",
                    Encoding.UTF8,
                    "application/json")
            });

        // Act
        CashierDto result = await _apiClient.LoginAsync("2468");

        // Assert
        result.CashierId.Should().Be(cashierId);
        result.Name.Should().Be("Cashier");
    }

    [Fact]
    public async Task StartShiftAsync_ShouldReturnServerShiftContext()
    {
        // Arrange
        Guid shiftId = Guid.NewGuid();
        Guid cashierId = Guid.NewGuid();
        Guid terminalId = Guid.NewGuid();
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Post
                    && request.RequestUri == new Uri("http://localhost/api/shift/start")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    $$"""
                    {
                      "shiftId": "{{shiftId}}",
                      "cashierId": "{{cashierId}}",
                      "terminalId": "{{terminalId}}",
                      "openedAt": "2026-07-15T12:00:00Z",
                      "status": "OPEN"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        // Act
        ShiftDto result = await _apiClient.StartShiftAsync(new StartShiftRequest
        {
            CashierId = cashierId,
            TerminalId = terminalId,
            OpeningCashPaisa = 10_000
        });

        // Assert
        result.ShiftId.Should().Be(shiftId);
        result.CashierId.Should().Be(cashierId);
        result.TerminalId.Should().Be(terminalId);
        result.Status.Should().Be("OPEN");
    }

    [Fact]
    public async Task CompleteSaleAsync_ShouldThrowWebPosClientException_WhenServerReturns400()
    {
        // Arrange
        const string errorBody = """{"message": "Invalid amount"}""";
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(errorBody, Encoding.UTF8, "application/json")
            });

        // Act
        Func<Task> act = () => _apiClient.CompleteSaleAsync(CreateValidSaleRequest("INV-400"));

        // Assert
        var exception = await act.Should().ThrowAsync<WebPosClientException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        exception.Which.ResponseBody.Should().Be(errorBody);
    }

    [Fact]
    public async Task CompleteSaleAsync_ShouldReturnData_WhenResponseIsValid()
    {
        // Arrange
        CompleteSaleResult expectedResult = new()
        {
            InvoiceNo = "TEST-123",
            ReceiptNumber = "TEST-123",
            TotalAmountPaisa = 30_000L,
            GrossAmountPaisa = 30_000L,
            DiscountAmountPaisa = 0L,
            TransactionGroupId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")
        };
        string json = JsonSerializer.Serialize(expectedResult);

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Act
        CompleteSaleResult result = await _apiClient.CompleteSaleAsync(CreateValidSaleRequest("TEST-123"));

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
    }

    [Fact]
    public async Task GetProductsForSaleAsync_ShouldReturnPricingAndStockData()
    {
        // Arrange
        const string responseJson = """
            [
              {
                "productId": "11111111-1111-1111-1111-111111111111",
                "batchId": "22222222-2222-2222-2222-222222222222",
                "batchNumber": "BATCH-1",
                "name": "Test Product",
                "sku": "SKU-1",
                "barcode": "123456",
                "unitPricePaisa": 15000,
                "availableStock": 12.5,
                "expiryDate": "2030-12-31"
              }
            ]
            """;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get
                    && request.RequestUri == new Uri("http://localhost/api/products/for-sale")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        IReadOnlyList<SalesProductDto> products = await _apiClient.GetProductsForSaleAsync();

        // Assert
        products.Should().ContainSingle();
        products[0].UnitPricePaisa.Should().Be(15_000L);
        products[0].AvailableStock.Should().Be(12.5m);
    }

    private static string CreateValidSaleResultJson(string invoiceNo) =>
        $$"""
        {
          "invoiceNo": "{{invoiceNo}}",
          "receiptNumber": "{{invoiceNo}}",
          "totalAmountPaisa": 15000,
          "grossAmountPaisa": 15000,
          "discountAmountPaisa": 0,
          "transactionGroupId": "11111111-2222-3333-4444-555555555555"
        }
        """;

    private static CompleteSaleRequest CreateValidSaleRequest(string invoiceNo) =>
        new()
        {
            InvoiceNo = invoiceNo,
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
                    BatchNumber = "BATCH-1",
                    ProductName = "Test Product",
                    Quantity = 2m,
                    UnitPricePaisa = 15_000L,
                    DiscountAppliedPaisa = 0L
                }
            ]
        };
}
