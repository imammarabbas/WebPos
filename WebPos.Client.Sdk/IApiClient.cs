using Common.Models;

namespace WebPos.Client.Sdk;

public interface IApiClient
{
    Task<EnrollmentCertificateDto> EnrollTerminalAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default);

    Task<EnrollmentPublicKeyDto> GetEnrollmentPublicKeyAsync(
        CancellationToken cancellationToken = default);

    Task<CashierDto> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default);

    Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<OpenShiftDto?> GetOpenShiftAsync(
        Guid? terminalId = null,
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> ForceCloseShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> CloseShiftAsync(
        CloseShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<CompleteSaleResult> CompleteSaleAsync(
        CompleteSaleRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesProductDto>> GetProductsForSaleAsync(
        CancellationToken cancellationToken = default);

    Task<SalesProductDto> GetProductByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesProductDto>> GetProductsForReceiveAsync(
        CancellationToken cancellationToken = default);

    Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyDto>> GetPartiesAsync(
        string? role = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummaryDto>> GetInvoicesAsync(
        Guid? shiftId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummaryDto>> SearchInvoicesAsync(
        string? invoice = null,
        string? customer = null,
        string? product = null,
        int limit = 40,
        CancellationToken cancellationToken = default);

    Task<SalesInvoiceDetailDto> GetInvoiceAsync(
        string invoiceNo,
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> GetShiftReconciliationAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<ManagerPinVerifiedDto> VerifyManagerPinAsync(
        string pin,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResultDto> QuickReceiveAsync(
        QuickReceiveRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResultDto> DirectReceiveAsync(
        QuickReceiveRequestDto request,
        CancellationToken cancellationToken = default);

    Task<StoreStatusDto> GetStoreStatusAsync(
        CancellationToken cancellationToken = default);
}
