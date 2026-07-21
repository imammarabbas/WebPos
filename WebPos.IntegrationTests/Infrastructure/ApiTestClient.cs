using System.Net.Http.Json;
using System.Text.Json;
using WebPos.Filters;

namespace WebPos.IntegrationTests.Infrastructure;

/// <summary>
/// Sends versioned, enrollment-authenticated API requests without reusing
/// request state that breaks sequential HttpClient calls in integration tests.
/// </summary>
internal static class ApiTestClient
{
    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        SalesApiFactory factory,
        HttpMethod method,
        string path,
        Guid tenantId,
        Guid terminalId,
        object? body = null,
        bool includeVersionHeader = true,
        bool includeEnrollmentAuth = true)
    {
        HttpRequestMessage request = new(method, path);

        if (includeVersionHeader)
        {
            request.Headers.TryAddWithoutValidation(ApiVersionFilter.HeaderName, "1.0.0");
        }

        if (includeEnrollmentAuth)
        {
            factory.ApplyEnrollmentAuth(request, tenantId, terminalId);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    public static async Task<T?> ReadJsonAsync<T>(
        HttpResponseMessage response,
        JsonSerializerOptions? options = null)
    {
        string payload = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload, options ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
