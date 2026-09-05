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

    public Task<EnrollmentPublicKeyDto> GetEnrollmentPublicKeyAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<EnrollmentPublicKeyDto>(
            HttpMethod.Get,
            "api/terminal-enrollment/public-key",
            content: null,
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

    public async Task<OpenShiftDto?> GetOpenShiftAsync(
        Guid? terminalId = null,
        CancellationToken cancellationToken = default)
    {
        string path = terminalId is Guid id && id != Guid.Empty
            ? $"api/shift/open?terminalId={id:D}"
            : "api/shift/open";

        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(ApiVersionHeaderName, "1.0.0");

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

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }

            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new WebPosClientException(
                    response.StatusCode,
                    $"WebPos API call to '{path}' failed with {(int)response.StatusCode} ({response.StatusCode}).",
                    responseBody: responseBody,
                    requestPath: path);
            }

            return JsonSerializer.Deserialize<OpenShiftDto>(responseBody, JsonOptions);
        }
    }

    public Task<CashVarianceReport> ForceCloseShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id is required.", nameof(shiftId));
        }

        return SendAsync<CashVarianceReport>(
            HttpMethod.Post,
            $"api/shift/{shiftId:D}/force-close",
            content: null,
            cancellationToken);
    }

    public Task<CashVarianceReport> CloseShiftAsync(
        CloseShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<CashVarianceReport>(
            HttpMethod.Post,
            "api/shift/close",
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

    public Task<SalesProductDto> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);
        string encoded = Uri.EscapeDataString(barcode.Trim());
        return SendAsync<SalesProductDto>(
            HttpMethod.Get,
            $"api/products/by-barcode/{encoded}",
            content: null,
            cancellationToken);
    }

    public Task<IReadOnlyList<SalesProductDto>> GetProductsForReceiveAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<SalesProductDto>>(
            HttpMethod.Get,
            "api/products/for-receive",
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

    public Task<IReadOnlyList<PartyDto>> GetPartiesAsync(
        string? role = null,
        CancellationToken cancellationToken = default)
    {
        string path = string.IsNullOrWhiteSpace(role)
            ? "api/parties"
            : $"api/parties?role={Uri.EscapeDataString(role)}";

        return SendAsync<IReadOnlyList<PartyDto>>(
            HttpMethod.Get,
            path,
            content: null,
            cancellationToken);
    }

    public Task<IReadOnlyList<SalesInvoiceSummaryDto>> GetInvoicesAsync(
        Guid? shiftId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        int clamped = Math.Clamp(limit, 1, 200);
        string path = shiftId is Guid id && id != Guid.Empty
            ? $"api/sales/invoices?shiftId={id:D}&limit={clamped}"
            : $"api/sales/invoices?limit={clamped}";

        return SendAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(
            HttpMethod.Get,
            path,
            content: null,
            cancellationToken);
    }

    public Task<IReadOnlyList<SalesInvoiceSummaryDto>> SearchInvoicesAsync(
        string? invoice = null,
        string? customer = null,
        string? product = null,
        int limit = 40,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>
        {
            $"limit={Math.Clamp(limit, 1, 200)}"
        };

        if (!string.IsNullOrWhiteSpace(invoice))
        {
            parts.Add($"invoice={Uri.EscapeDataString(invoice.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            parts.Add($"customer={Uri.EscapeDataString(customer.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(product))
        {
            parts.Add($"product={Uri.EscapeDataString(product.Trim())}");
        }

        return SendAsync<IReadOnlyList<SalesInvoiceSummaryDto>>(
            HttpMethod.Get,
            $"api/sales/invoices/search?{string.Join('&', parts)}",
            content: null,
            cancellationToken);
    }

    public Task<SalesInvoiceDetailDto> GetInvoiceAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNo);
        return SendAsync<SalesInvoiceDetailDto>(
            HttpMethod.Get,
            $"api/sales/invoices/{Uri.EscapeDataString(invoiceNo)}",
            content: null,
            cancellationToken);
    }

    public Task<CashVarianceReport> GetShiftReconciliationAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty)
        {
            throw new ArgumentException("Shift id is required.", nameof(shiftId));
        }

        return SendAsync<CashVarianceReport>(
            HttpMethod.Get,
            $"api/shift/{shiftId:D}/reconciliation",
            content: null,
            cancellationToken);
    }

    public Task<ManagerPinVerifiedDto> VerifyManagerPinAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);
        return SendAsync<ManagerPinVerifiedDto>(
            HttpMethod.Post,
            "api/auth/verify-manager-pin",
            new LoginRequest { Pin = pin },
            cancellationToken);
    }

    public Task<ReceiveStockResultDto> QuickReceiveAsync(
        QuickReceiveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<ReceiveStockResultDto>(
            HttpMethod.Post,
            "api/purchases/quick-receive",
            request,
            cancellationToken);
    }

    public Task<ReceiveStockResultDto> DirectReceiveAsync(
        QuickReceiveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync<ReceiveStockResultDto>(
            HttpMethod.Post,
            "api/purchases/direct-receive",
            request,
            cancellationToken);
    }

    public Task<StoreStatusDto> GetStoreStatusAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<StoreStatusDto>(
            HttpMethod.Get,
            "api/store/status",
            content: null,
            cancellationToken);

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
                string? detail = TryReadProblemDetail(responseBody);
                string message = string.IsNullOrWhiteSpace(detail)
                    ? $"WebPos API call to '{path}' failed with {(int)response.StatusCode} ({response.StatusCode})."
                    : detail;

                throw new WebPosClientException(
                    response.StatusCode,
                    message,
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

    private static string? TryReadProblemDetail(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("detail", out JsonElement detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (doc.RootElement.TryGetProperty("title", out JsonElement title)
                && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            // Keep generic status message when body is not ProblemDetails JSON.
        }

        return null;
    }
}
