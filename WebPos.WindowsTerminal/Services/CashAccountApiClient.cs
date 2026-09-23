using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

public sealed class CashAccountApiClient(
    IApiClient apiClient,
    ILogger<CashAccountApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<CashAccountApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<IReadOnlyList<PaymentAccountDto>>> ListPaymentAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<PaymentAccountDto> accounts =
                await _apiClient.ListPaymentAccountsAsync(cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<PaymentAccountDto>>.Ok(accounts);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "WebPos API failed ({StatusCode}) listing payment accounts: {Message}",
                ex.StatusCode,
                ex.Message);
            return Result<IReadOnlyList<PaymentAccountDto>>.Fail(ex.Message);
        }
        catch (ApiDeserializationException ex)
        {
            _logger.LogError(ex, "Payment accounts response could not be read.");
            return Result<IReadOnlyList<PaymentAccountDto>>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected failure listing payment accounts.");
            return Result<IReadOnlyList<PaymentAccountDto>>.Fail(ex.Message);
        }
    }
}
