using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for supplier receive / manager PIN APIs.</summary>
public sealed class PurchaseIntakeApiClient(IApiClient apiClient, ILogger<PurchaseIntakeApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<PurchaseIntakeApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<IReadOnlyList<PartyDto>>> GetSuppliersAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<PartyDto> suppliers =
                await _apiClient.GetPartiesAsync("SUPPLIER", cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<PartyDto>>.Ok(suppliers);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to load suppliers.");
            return Result<IReadOnlyList<PartyDto>>.Fail(ex.Message);
        }
    }

    public async Task<Result<bool>> VerifyManagerPinAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ManagerPinVerifiedDto result =
                await _apiClient.VerifyManagerPinAsync(pin, cancellationToken).ConfigureAwait(false);
            return Result<bool>.Ok(result.Verified);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogWarning(ex, "Manager PIN verification failed.");
            return Result<bool>.Fail(ex.Message);
        }
    }

    public async Task<Result<ReceiveStockResultDto>> QuickReceiveAsync(
        QuickReceiveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ReceiveStockResultDto result =
                await _apiClient.DirectReceiveAsync(request, cancellationToken).ConfigureAwait(false);
            return Result<ReceiveStockResultDto>.Ok(result);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Quick receive failed ({StatusCode}).", ex.StatusCode);
            return Result<ReceiveStockResultDto>.Fail(ex.Message, ex.StatusCode);
        }
    }
}
