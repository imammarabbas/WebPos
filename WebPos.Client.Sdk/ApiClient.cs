using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Common.Models;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.Client.Sdk;

public sealed class ApiClient : IApiClient
{
    public const string HttpClientName = "WebPosApi";
    public const string ApiVersionHeaderName = "X-Api-Version";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public Task<EnrollmentCertificateDto> EnrollTerminalAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<EnrollmentCertificateDto>(
            HttpMethod.Post,
            "api/terminal-enrollment",
            request,
            cancellationToken);
    }

    public Task<CashierDto> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);
        return SendAsync<CashierDto>(
            HttpMethod.Post,
            "api/auth/login",
            new LoginRequest { Pin = pin },
            cancellationToken);
    }

    public Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<ShiftDto>(
            HttpMethod.Post,
            "api/shift/start",
            request,
            cancellationToken);
    }

    public Task<CompleteSaleResult> CompleteSaleAsync(
        CompleteSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<CompleteSaleResult>(
            HttpMethod.Post,
            "api/sales/complete",
            request,
            cancellationToken);
    }

    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<ProductDto>>(
            HttpMethod.Get,
            "api/products",
            content: null,
            cancellationToken);

    public Task<IReadOnlyList<SalesProductDto>> GetProductsForSaleAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SalesProductDto>>(
            HttpMethod.Get,
            "api/products/for-sale",
            content: null,
            cancellationToken);

    public Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<ReturnItemsResult>(
            HttpMethod.Post,
            "api/sales/return",
            request,
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? content,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.TryAddWithoutValidation(ApiVersionHeaderName, "1.0.0");

        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new WebPosClientException(
                HttpStatusCode.ServiceUnavailable,
                $"Failed to reach WebPos API at '{path}'.",
                requestPath: path,
                innerException: ex);
        }
        catch (OperationCanceledException ex)
        {
            throw new WebPosClientException(
                HttpStatusCode.RequestTimeout,
                $"WebPos API request to '{path}' timed out or was cancelled.",
                requestPath: path,
                innerException: ex);
        }

        using (response)
        {
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new WebPosClientException(
                    response.StatusCode,
                    $"WebPos API call to '{path}' failed with {(int)response.StatusCode} ({response.StatusCode}).",
                    responseBody: responseBody,
                    requestPath: path);
            }

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                throw new ApiDeserializationException(
                    typeof(TResponse),
                    $"WebPos API call to '{path}' returned an empty response body.",
                    responseBody: responseBody);
            }

            try
            {
                TResponse? payload = JsonSerializer.Deserialize<TResponse>(responseBody, JsonOptions);
                if (payload is null)
                {
                    throw new ApiDeserializationException(
                        typeof(TResponse),
                        $"WebPos API call to '{path}' deserialized to null.",
                        responseBody: responseBody);
                }

                return payload;
            }
            catch (JsonException ex)
            {
                throw new ApiDeserializationException(
                    typeof(TResponse),
                    $"Failed to deserialize WebPos API response from '{path}' as {typeof(TResponse).Name}.",
                    responseBody: responseBody,
                    innerException: ex);
            }
        }
    }
}
