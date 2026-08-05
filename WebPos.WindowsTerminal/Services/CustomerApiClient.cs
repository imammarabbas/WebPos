using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for customer party APIs.</summary>
public sealed class CustomerApiClient(IApiClient apiClient, ILogger<CustomerApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<CustomerApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<IReadOnlyList<PartyDto>>> GetCustomersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<PartyDto> customers =
                await _apiClient.GetPartiesAsync("CUSTOMER", cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<PartyDto>>.Ok(customers);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to load customers ({StatusCode}): {Message}",
                ex.StatusCode,
                ex.Message);
            return Result<IReadOnlyList<PartyDto>>.Fail(ex.Message);
        }
    }

    public static IEnumerable<PartyDto> FilterCustomers(
        IReadOnlyList<PartyDto> customers,
        string search)
    {
        string trimmed = search.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return customers.OrderBy(customer => customer.Name);
        }

        return customers
            .Where(customer =>
                customer.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || customer.PhoneNumber.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(customer => customer.Name);
    }
}
