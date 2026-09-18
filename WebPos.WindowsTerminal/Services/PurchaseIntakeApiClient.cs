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

    public async Task<Result<PartyDto>> CreateSupplierAsync(
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
                    Role = "SUPPLIER",
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
                "Failed to create supplier ({StatusCode}): {Message}",
                ex.StatusCode,
                ex.Message);
            return Result<PartyDto>.Fail(ex.Message);
        }
    }

    public async Task<Result<PartyDto>> UpdateSupplierAsync(
        Guid supplierId,
        string name,
        string phoneNumber,
        string? address = null,
        long? creditLimitPaisa = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PartyDto updated = await _apiClient.UpdatePartyAsync(
                supplierId,
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
                "Failed to update supplier {SupplierId} ({StatusCode}): {Message}",
                supplierId,
                ex.StatusCode,
                ex.Message);
            return Result<PartyDto>.Fail(ex.Message);
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

    public async Task<Result<CreatePurchaseOrderResultDto>> CreatePurchaseAsync(
        CreatePurchaseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            CreatePurchaseOrderResultDto result =
                await _apiClient.CreatePurchaseAsync(request, cancellationToken).ConfigureAwait(false);
            return Result<CreatePurchaseOrderResultDto>.Ok(result);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Create purchase failed ({StatusCode}).", ex.StatusCode);
            return Result<CreatePurchaseOrderResultDto>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<Result<IReadOnlyList<PurchaseOrderSummaryDto>>> ListPurchasesAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<PurchaseOrderSummaryDto> orders =
                await _apiClient.GetPurchasesAsync(limit, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<PurchaseOrderSummaryDto>>.Ok(orders);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list purchases.");
            return Result<IReadOnlyList<PurchaseOrderSummaryDto>>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<Result<PurchaseOrderDetailDto>> GetPurchaseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PurchaseOrderDetailDto detail =
                await _apiClient.GetPurchaseAsync(purchaseOrderId, cancellationToken).ConfigureAwait(false);
            return Result<PurchaseOrderDetailDto>.Ok(detail);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to load purchase {PurchaseOrderId}.", purchaseOrderId);
            return Result<PurchaseOrderDetailDto>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<Result<PurchaseOrderDetailDto>> UpdatePurchaseAsync(
        Guid purchaseOrderId,
        UpdateOpenPurchaseRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            PurchaseOrderDetailDto detail = await _apiClient
                .UpdatePurchaseAsync(purchaseOrderId, request, cancellationToken)
                .ConfigureAwait(false);
            return Result<PurchaseOrderDetailDto>.Ok(detail);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Update purchase {PurchaseOrderId} failed ({StatusCode}).", purchaseOrderId, ex.StatusCode);
            return Result<PurchaseOrderDetailDto>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<Result<ReceiveStockResultDto>> ReceivePurchaseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ReceiveStockResultDto result = await _apiClient
                .ReceivePurchaseAsync(purchaseOrderId, cancellationToken)
                .ConfigureAwait(false);
            return Result<ReceiveStockResultDto>.Ok(result);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Receive purchase {PurchaseOrderId} failed ({StatusCode}).", purchaseOrderId, ex.StatusCode);
            return Result<ReceiveStockResultDto>.Fail(ex.Message, ex.StatusCode);
        }
    }
}
