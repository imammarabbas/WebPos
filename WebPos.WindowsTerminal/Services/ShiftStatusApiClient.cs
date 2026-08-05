using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for shift reconciliation / expected cash.</summary>
public sealed class ShiftStatusApiClient(
    IApiClient apiClient,
    ISessionService sessionService,
    ILogger<ShiftStatusApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ISessionService _sessionService =
        sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly ILogger<ShiftStatusApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public long ExpectedCashPaisa { get; private set; }

    public bool IsApiOnline { get; private set; } = true;

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_sessionService.IsActive)
        {
            ExpectedCashPaisa = 0;
            Changed?.Invoke();
            return;
        }

        try
        {
            CashVarianceReport report = await _apiClient
                .GetShiftReconciliationAsync(_sessionService.ShiftId, cancellationToken)
                .ConfigureAwait(false);
            ExpectedCashPaisa = report.ExpectedCashPaisa;
            IsApiOnline = true;
            _sessionService.UpdateExpectedCash(report.ExpectedCashPaisa);
        }
        catch (WebPosClientException ex)
        {
            IsApiOnline = false;
            _logger.LogWarning(ex, "Failed to refresh shift reconciliation.");
        }
        catch (Exception ex)
        {
            IsApiOnline = false;
            _logger.LogWarning(ex, "Failed to refresh shift reconciliation.");
        }

        Changed?.Invoke();
    }

    public void ApplyLocalCashDelta(long paisaDelta)
    {
        ExpectedCashPaisa = Math.Max(0, ExpectedCashPaisa + paisaDelta);
        _sessionService.UpdateExpectedCash(ExpectedCashPaisa);
        Changed?.Invoke();
    }
}
