using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

public sealed class LoginService(IApiClient apiClient, ILogger<LoginService> logger)
{
    private readonly IApiClient _apiClient =
        apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<LoginService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public CashierDto? CurrentCashier { get; private set; }

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
                    : ex.Message);
        }
    }
}
