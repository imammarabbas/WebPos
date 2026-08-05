using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>Thin HTTP client for invoice list / detail / return APIs.</summary>
public sealed class InvoiceApiClient(IApiClient apiClient, ILogger<InvoiceApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ILogger<InvoiceApiClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<Result<IReadOnlyList<SalesInvoiceSummaryDto>>> ListAsync(
        Guid shiftId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SalesInvoiceSummaryDto> invoices =
                await _apiClient.GetInvoicesAsync(shiftId, limit, cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Ok(invoices);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list invoices.");
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<SalesInvoiceSummaryDto>>> ListRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SalesInvoiceSummaryDto> invoices =
                await _apiClient.GetInvoicesAsync(shiftId: null, limit, cancellationToken)
                    .ConfigureAwait(false);
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Ok(invoices);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to list recent invoices.");
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<SalesInvoiceSummaryDto>>> SearchAsync(
        string? invoice = null,
        string? customer = null,
        string? product = null,
        int limit = 40,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SalesInvoiceSummaryDto> invoices =
                await _apiClient.SearchInvoicesAsync(
                    invoice,
                    customer,
                    product,
                    limit,
                    cancellationToken).ConfigureAwait(false);
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Ok(invoices);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to search invoices.");
            return Result<IReadOnlyList<SalesInvoiceSummaryDto>>.Fail(ex.Message);
        }
    }

    public async Task<Result<SalesInvoiceDetailDto>> GetAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            SalesInvoiceDetailDto detail =
                await _apiClient.GetInvoiceAsync(invoiceNo, cancellationToken).ConfigureAwait(false);
            return Result<SalesInvoiceDetailDto>.Ok(detail);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Failed to load invoice {InvoiceNo}.", invoiceNo);
            return Result<SalesInvoiceDetailDto>.Fail(ex.Message);
        }
    }

    public async Task<Result<ReturnItemsResult>> ReturnAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ReturnItemsResult result =
                await _apiClient.ReturnItemsAsync(request, cancellationToken).ConfigureAwait(false);
            return Result<ReturnItemsResult>.Ok(result);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(ex, "Return failed for {InvoiceNo}.", request.OriginalInvoiceNo);
            return Result<ReturnItemsResult>.Fail(ex.Message);
        }
    }
}
