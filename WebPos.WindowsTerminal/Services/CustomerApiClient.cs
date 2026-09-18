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

    public async Task<Result<PartyDto>> CreateCustomerAsync(
        string name,
        string phoneNumber,
        string? address = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PartyDto created = await _apiClient.CreatePartyAsync(
                new CreatePartyRequest
                {
                    Role = "CUSTOMER",
                    Name = name.Trim(),
                    PhoneNumber = phoneNumber.Trim(),
                    Address = address?.Trim() ?? string.Empty,
                    CreditLimitPaisa = 0
                },
                cancellationToken).ConfigureAwait(false);
            return Result<PartyDto>.Ok(created);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to create customer ({StatusCode}): {Message}",
                ex.StatusCode,
                ex.Message);
            return Result<PartyDto>.Fail(ex.Message);
        }
    }

    public async Task<Result<PartyDto>> UpdateCustomerAsync(
        Guid customerId,
        string name,
        string phoneNumber,
        string? address = null,
        long? creditLimitPaisa = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PartyDto updated = await _apiClient.UpdatePartyAsync(
                customerId,
                new UpdatePartyRequest
                {
                    Name = name.Trim(),
                    PhoneNumber = phoneNumber.Trim(),
                    Address = address?.Trim() ?? string.Empty,
                    CreditLimitPaisa = creditLimitPaisa ?? 0
                },
                cancellationToken).ConfigureAwait(false);
            return Result<PartyDto>.Ok(updated);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to update customer {CustomerId} ({StatusCode}): {Message}",
                customerId,
                ex.StatusCode,
                ex.Message);
            return Result<PartyDto>.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<PartyLedgerEntryDto>>> GetLedgerAsync(
        Guid customerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 500,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<PartyLedgerEntryDto> entries = await _apiClient
                .GetPartyLedgerAsync(customerId, from, to, limit, cancellationToken)
                .ConfigureAwait(false);
            return Result<IReadOnlyList<PartyLedgerEntryDto>>.Ok(entries);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "Failed to load ledger for customer {CustomerId} ({StatusCode}): {Message}",
                customerId,
                ex.StatusCode,
                ex.Message);
            return Result<IReadOnlyList<PartyLedgerEntryDto>>.Fail(ex.Message);
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
