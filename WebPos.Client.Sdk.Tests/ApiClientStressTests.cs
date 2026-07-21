using System.Net;
using System.Text;
using System.Text.Json;
using Common.Models;
using FluentAssertions;
using RichardSzalay.MockHttp;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.Client.Sdk.Tests;

/// <summary>
/// Deterministic performance / resiliency checks for <see cref="ApiClient"/>
/// using <see cref="MockHttpMessageHandler"/> (no real network I/O).
/// </summary>
public sealed class ApiClientStressTests
{
    private const string BaseAddress = "http://localhost/";

    [Fact]
    public async Task GetProductsAsync_ShouldHandleFiftyConcurrentRequests()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        MockedRequest expectation = mockHttp
            .When(HttpMethod.Get, $"{BaseAddress}api/products")
            .Respond("application/json", "[]");

        using HttpClient httpClient = CreateHttpClient(mockHttp);
        ApiClient sut = new(httpClient);

        // Act
        Task<IReadOnlyList<ProductDto>>[] tasks = Enumerable
            .Range(0, 50)
            .Select(_ => sut.GetProductsAsync())
            .ToArray();

        IReadOnlyList<ProductDto>[] results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(50);
        results.Should().OnlyContain(r => r.Count == 0);
        mockHttp.GetMatchCount(expectation).Should().Be(50);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldThrowRequestTimeout_WhenCancellationTokenExpires()
    {
        // Arrange
        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .When(HttpMethod.Get, $"{BaseAddress}api/products")
            .Respond(async _ =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("[]", Encoding.UTF8, "application/json")
                };
            });

        using HttpClient httpClient = CreateHttpClient(mockHttp);
        // Keep HttpClient timeout high so only the caller's token cancels first.
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        ApiClient sut = new(httpClient);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(1));

        // Act
        Func<Task> act = () => sut.GetProductsAsync(cts.Token);

        // Assert
        (await act.Should().ThrowAsync<WebPosClientException>())
            .Which.Should().Match<WebPosClientException>(ex =>
                ex.StatusCode == HttpStatusCode.RequestTimeout
                && ex.RequestPath == "api/products"
                && ex.InnerException is OperationCanceledException);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldDisposeHttpResponseMessage()
    {
        // Arrange
        TrackingHttpResponseMessage trackedResponse = new()
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        };

        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .When(HttpMethod.Get, $"{BaseAddress}api/products")
            .Respond(_ => trackedResponse);

        using HttpClient httpClient = CreateHttpClient(mockHttp);
        ApiClient sut = new(httpClient);

        // Act
        _ = await sut.GetProductsAsync();

        // Assert
        trackedResponse.WasDisposed.Should().BeTrue(
            "ApiClient must dispose HttpResponseMessage via its using block in SendAsync");
    }

    [Fact]
    public async Task GetProductsAsync_ShouldDeserializeLargePayloadWithoutCrashing()
    {
        // Arrange
        const int productCount = 10_000;
        List<ProductDto> products = Enumerable
            .Range(0, productCount)
            .Select(i => new ProductDto
            {
                Id = Guid.NewGuid(),
                Name = $"Product-{i}",
                Sku = $"SKU-{i:D6}",
                Barcode = $"BC-{i:D8}",
                Brand = "StressBrand",
                BaseUnit = "PCS",
                ConversionMultiplier = 1,
                ShowOnWebshop = i % 2 == 0
            })
            .ToList();

        string largeJson = JsonSerializer.Serialize(products);
        largeJson.Length.Should().BeGreaterThan(500_000, "payload should be large enough to stress deserialization");

        MockHttpMessageHandler mockHttp = new();
        mockHttp
            .When(HttpMethod.Get, $"{BaseAddress}api/products")
            .Respond("application/json", largeJson);

        using HttpClient httpClient = CreateHttpClient(mockHttp);
        ApiClient sut = new(httpClient);

        // Act
        IReadOnlyList<ProductDto> result = await sut.GetProductsAsync();

        // Assert
        result.Should().HaveCount(productCount);
        result[0].Name.Should().StartWith("Product-");
        result[^1].Sku.Should().Be($"SKU-{productCount - 1:D6}");
    }

    private static HttpClient CreateHttpClient(MockHttpMessageHandler mockHttp)
    {
        HttpClient client = mockHttp.ToHttpClient();
        client.BaseAddress = new Uri(BaseAddress);
        return client;
    }

    /// <summary>
    /// Tracks whether <see cref="Dispose(bool)"/> was invoked so tests can assert
    /// <see cref="ApiClient"/> disposes responses correctly.
    /// </summary>
    private sealed class TrackingHttpResponseMessage : HttpResponseMessage
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
