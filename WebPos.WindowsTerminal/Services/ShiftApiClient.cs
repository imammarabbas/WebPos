using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for shift start/close APIs.</summary>
public sealed class ShiftApiClient(IApiClient apiClient, ILogger<ShiftApiClient> logger)
{
    private readonly IApiClient _apiClient =
        apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<ShiftApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<ShiftDto>> StartShiftAsync(
        Guid cashierId,
        Guid terminalId,
        long openingCashPaisa,
        CancellationToken cancellationToken = default)
    {
        if (cashierId == Guid.Empty || terminalId == Guid.Empty)
        {
            return Result<ShiftDto>.Fail("Cashier and terminal are required.");
        }

        try
        {
            ShiftDto shift = await _apiClient.StartShiftAsync(
                new StartShiftRequest
                {
                    CashierId = cashierId,
                    TerminalId = terminalId,
                    OpeningCashPaisa = openingCashPaisa
                },
                cancellationToken);

            return Result<ShiftDto>.Ok(shift);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogWarning(
                "Start shift failed with status {StatusCode}.",
                ex.StatusCode);
            if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                return Result<ShiftDto>.Fail(
                    "This terminal already has an open shift for another operator. Close it first or Force Close from Master → Open shifts.");
            }

            return Result<ShiftDto>.Fail(ex.Message);
        }
    }

    public async Task<Result<CashVarianceReport>> CloseShiftAsync(
        Guid shiftId,
        Guid cashierId,
        Guid terminalId,
        long actualCashPaisa,
        CancellationToken cancellationToken = default)
    {
        if (shiftId == Guid.Empty || cashierId == Guid.Empty || terminalId == Guid.Empty)
        {
            return Result<CashVarianceReport>.Fail(
                "Shift, cashier, and terminal are required.");
        }

        if (actualCashPaisa < 0)
        {
            return Result<CashVarianceReport>.Fail("Actual cash cannot be negative.");
        }

        try
        {
            CashVarianceReport report = await _apiClient.CloseShiftAsync(
                new CloseShiftRequest
                {
                    ShiftId = shiftId,
                    CashierId = cashierId,
                    TerminalId = terminalId,
                    ActualCashPaisa = actualCashPaisa
                },
                cancellationToken);

            return Result<CashVarianceReport>.Ok(report);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogWarning(
                "Close shift failed with status {StatusCode}.",
                ex.StatusCode);
            return Result<CashVarianceReport>.Fail(ex.Message);
        }
    }
}
