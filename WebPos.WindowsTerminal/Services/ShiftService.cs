using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

public sealed class ShiftService(IApiClient apiClient, ILogger<ShiftService> logger)
{
    private readonly IApiClient _apiClient =
        apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<ShiftService> _logger =
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
            return Result<ShiftDto>.Fail(ex.Message);
        }
    }
}
