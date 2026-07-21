using Common.Models;
using Microsoft.Extensions.Logging;
using WebPos.Client.Sdk;
using WebPos.Client.Sdk.Exceptions;

namespace WebPos.WindowsTerminal.Services;

/// <summary>
/// Thin service wrapper over <see cref="IApiClient"/>. API failures are converted to
/// <see cref="Result{T}"/> values so UI components do not handle SDK exceptions.
/// </summary>
public sealed class SalesService(
    IApiClient apiClient,
    ISessionService sessionService,
    ILogger<SalesService> logger)
{
    private readonly IApiClient _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly ISessionService _sessionService =
        sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly ILogger<SalesService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

        SaleLineRequest? invalidLine = request.Lines.FirstOrDefault(line => line.UnitPricePaisa <= 0);
        if (invalidLine is not null)
        {
            return Task.FromResult(Result<CompleteSaleResult>.Fail(
                $"A valid unit price is required for {invalidLine.ProductName}."));
        }

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

        Result<IReadOnlyList<SalesProductDto>> productsResult =
            await GetProductsForSaleAsync(cancellationToken);

        if (!productsResult.Success || productsResult.Data is null)
        {
            return Result<CompleteSaleResult>.Fail(productsResult.ErrorMessage);
        }

        SalesProductDto? currentProduct = productsResult.Data.FirstOrDefault(
            product => product.BatchId == selectedProduct.BatchId);

        if (currentProduct is null)
        {
            return Result<CompleteSaleResult>.Fail(
                "This product is no longer available. Refresh the product list.");
        }

        if (currentProduct.UnitPricePaisa <= 0)
        {
            return Result<CompleteSaleResult>.Fail(
                $"A valid unit price is required for {currentProduct.Name}.");
        }

        if (quantity > currentProduct.AvailableStock)
        {
            return Result<CompleteSaleResult>.Fail(
                $"Only {currentProduct.AvailableStock:0.##} units of {currentProduct.Name} are available.");
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
                    ProductId = currentProduct.ProductId,
                    BatchId = currentProduct.BatchId,
                    BatchNumber = currentProduct.BatchNumber,
                    ProductName = currentProduct.Name,
                    Quantity = quantity,
                    UnitPricePaisa = currentProduct.UnitPricePaisa,
                    DiscountAppliedPaisa = 0
                }
            ]
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
