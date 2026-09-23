using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for cashier PIN login; holds current cashier for UI.</summary>
public sealed class LoginApiClient(IApiClient apiClient, ILogger<LoginApiClient> logger)
{
    private readonly IApiClient _apiClient =
        apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<LoginApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public CashierDto? CurrentCashier { get; private set; }

    public void ClearCashier() => CurrentCashier = null;

    public async Task<Result<CashierDto>> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return Result<CashierDto>.Fail("Enter your PIN.");
        }

        try
        {
            CashierDto cashier = await _apiClient.LoginAsync(pin, cancellationToken);
            CurrentCashier = cashier;
            return Result<CashierDto>.Ok(cashier);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogWarning(
                "Cashier login failed with status {StatusCode}.",
                ex.StatusCode);

            return Result<CashierDto>.Fail(
                ex.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "Invalid cashier PIN."
                    : ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                        or System.Net.HttpStatusCode.RequestTimeout
                        ? ex.Message
                        : ex.Message);
        }
        catch (OperationCanceledException)
        {
            return Result<CashierDto>.Fail("WebPos API request timed out or was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected cashier login failure.");
            return Result<CashierDto>.Fail(ex.Message);
        }
    }
}
