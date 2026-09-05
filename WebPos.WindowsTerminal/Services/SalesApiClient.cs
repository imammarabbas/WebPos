using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Thin HTTP client over <see cref="IApiClient"/> sales endpoints.
/// Domain rules (stock, price, discount, credit) are enforced by server Core — not here.
/// </summary>
public sealed class SalesApiClient(
    IApiClient apiClient,
    ISessionService sessionService,
    ILogger<SalesApiClient> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ISessionService _sessionService =
        sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly ILogger<SalesApiClient> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<Result<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteCoreAsync(action);
    }

    public Task<Result<CompleteSaleResult>> CompleteSaleAsync(
        CompleteSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureActiveSession();

        CompleteSaleRequest sessionRequest = new()
        {
            InvoiceNo = request.InvoiceNo,
            ShiftId = _sessionService.ShiftId,
            TerminalId = _sessionService.TerminalId,
            CashierId = _sessionService.CashierId,
            CustomerId = request.CustomerId,
            PaymentMethod = request.PaymentMethod,
            DiscountAmountPaisa = request.DiscountAmountPaisa,
            DiscountReason = request.DiscountReason,
            Lines = request.Lines
        };

        return ExecuteAsync(() => _apiClient.CompleteSaleAsync(sessionRequest, cancellationToken));
    }

    public Task<Result<IReadOnlyList<SalesProductDto>>> GetProductsForSaleAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _apiClient.GetProductsForSaleAsync(cancellationToken));

    public Task<Result<SalesProductDto>> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _apiClient.GetProductByBarcodeAsync(barcode, cancellationToken));

    public Task<Result<IReadOnlyList<SalesProductDto>>> GetProductsForReceiveAsync(
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _apiClient.GetProductsForReceiveAsync(cancellationToken));

    public async Task<Result<CompleteSaleResult>> CompleteSaleAsync(
        SalesProductDto selectedProduct,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedProduct);
        EnsureActiveSession();

        if (quantity <= 0)
        {
            return Result<CompleteSaleResult>.Fail("Quantity must be greater than zero.");
        }

        CompleteSaleRequest request = new()
        {
            InvoiceNo = $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            ShiftId = _sessionService.ShiftId,
            TerminalId = _sessionService.TerminalId,
            CashierId = _sessionService.CashierId,
            PaymentMethod = "CASH",
            DiscountAmountPaisa = 0,
            Lines =
            [
                new SaleLineRequest
                {
                    ProductId = selectedProduct.ProductId,
                    BatchId = selectedProduct.BatchId,
                    BatchNumber = selectedProduct.BatchNumber,
                    ProductName = selectedProduct.Name,
                    Quantity = quantity,
                    UnitPricePaisa = selectedProduct.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
        };

        return await CompleteSaleAsync(request, cancellationToken);
    }

    public async Task<Result<CompleteSaleResult>> CompleteCartAsync(
        IReadOnlyList<CartLine> lines,
        string paymentMethod,
        Guid? customerId = null,
        long discountAmountPaisa = 0,
        string? discountReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentMethod);
        EnsureActiveSession();

        if (lines.Count == 0)
        {
            return Result<CompleteSaleResult>.Fail("Cart is empty.");
        }

        List<SaleLineRequest> saleLines = [];
        foreach (CartLine line in lines)
        {
            if (line.Quantity <= 0)
            {
                return Result<CompleteSaleResult>.Fail(
                    $"Quantity must be greater than zero for {line.Product.Name}.");
            }

            saleLines.Add(new SaleLineRequest
            {
                ProductId = line.Product.ProductId,
                BatchId = line.Product.BatchId,
                BatchNumber = line.Product.BatchNumber,
                ProductName = line.Product.Name,
                Quantity = line.Quantity,
                UnitPricePaisa = line.Product.UnitPricePaisa,
                DiscountAppliedPaisa = 0
            });
        }

        CompleteSaleRequest request = new()
        {
            InvoiceNo = $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            ShiftId = _sessionService.ShiftId,
            TerminalId = _sessionService.TerminalId,
            CashierId = _sessionService.CashierId,
            CustomerId = customerId,
            PaymentMethod = paymentMethod.ToUpperInvariant(),
            DiscountAmountPaisa = discountAmountPaisa,
            DiscountReason = discountAmountPaisa > 0
                ? (string.IsNullOrWhiteSpace(discountReason) ? "POS discount" : discountReason)
                : null,
            Lines = saleLines
        };

        return await CompleteSaleAsync(request, cancellationToken);
    }

    private async Task<Result<T>> ExecuteCoreAsync<T>(Func<Task<T>> action)
    {
        try
        {
            T data = await action().ConfigureAwait(false);
            return Result<T>.Ok(data);
        }
        catch (WebPosClientException ex)
        {
            _logger.LogError(
                ex,
                "WebPos API failed ({StatusCode}) on {RequestPath}: {Message}",
                ex.StatusCode,
                ex.RequestPath,
                ex.Message);

            return Result<T>.Fail(ex.Message);
        }
    }

    private void EnsureActiveSession()
    {
        if (!_sessionService.IsActive)
        {
            throw new SessionNotActiveException();
        }
    }
}
