using Common.Models;

namespace WebPos.Client.Sdk;

public interface IApiClient
{
    Task<EnrollmentCertificateDto> EnrollTerminalAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default);

    Task<CashierDto> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default);

    Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<CompleteSaleResult> CompleteSaleAsync(
        CompleteSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesProductDto>> GetProductsForSaleAsync(
        CancellationToken cancellationToken = default);

    Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default);
}
