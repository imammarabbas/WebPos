using Common.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for shift reconciliation / expected cash and API reachability.</summary>
public sealed class ShiftStatusApiClient(
    IApiClient apiClient,
    ISessionService sessionService,
    IOptions<ApiClientOptions> apiOptions,
    ILogger<ShiftStatusApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ISessionService _sessionService =
        sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly ApiClientOptions _apiOptions =
        apiOptions?.Value ?? throw new ArgumentNullException(nameof(apiOptions));
    private readonly ILogger<ShiftStatusApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public long ExpectedCashPaisa { get; private set; }

    public bool IsApiOnline { get; private set; }

    public string ApiBaseAddress =>
        string.IsNullOrWhiteSpace(_apiOptions.BaseAddress)
            ? "http://localhost:8080/"
            : _apiOptions.BaseAddress.Trim();

    public event Action? Changed;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_sessionService.IsActive)
        {
            ExpectedCashPaisa = 0;
            await ProbeApiReachableAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>Lightweight anonymous probe used on the login screen when no shift is open.</summary>
    public async Task ProbeApiReachableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await _apiClient.GetEnrollmentPublicKeyAsync(cancellationToken).ConfigureAwait(false);
            IsApiOnline = true;
        }
        catch (Exception ex)
        {
            IsApiOnline = false;
            _logger.LogWarning(
                ex,
                "API unreachable at {BaseAddress}.",
                ApiBaseAddress);
        }
    }

    public void ApplyLocalCashDelta(long paisaDelta)
    {
        ExpectedCashPaisa = Math.Max(0, ExpectedCashPaisa + paisaDelta);
        _sessionService.UpdateExpectedCash(ExpectedCashPaisa);
        Changed?.Invoke();
    }
}
