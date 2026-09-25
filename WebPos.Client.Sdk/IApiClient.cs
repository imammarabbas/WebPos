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

    Task<CashierDto> LoginAsync(
        string pin,
        string? role,
        CancellationToken cancellationToken = default);

    Task<CashierDto> LoginWithPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<OpenShiftDto?> GetOpenShiftAsync(
        Guid? terminalId = null,
        CancellationToken cancellationToken = default);

    Task<SuggestedOpeningCashDto> GetSuggestedOpeningCashAsync(
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

    Task<PagedProductResult> SearchProductsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 50,
        bool lowStockOnly = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductDto>> GetBulkParentsAsync(
        string? search = null,
        int take = 50,
        Guid? includeId = null,
        CancellationToken cancellationToken = default);

    Task<ProductDto> GetProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProductDto> CreateProductAsync(
        UpsertProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductDto> UpdateProductAsync(
        Guid productId,
        UpsertProductRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesProductDto>> GetProductsForSaleAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleMasterDto>> GetSaleMastersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SaleQuickLinkDto>> GetSaleQuickLinksAsync(
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

    Task<PartyDto> CreatePartyAsync(
        CreatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<PartyDto> UpdatePartyAsync(
        Guid partyId,
        UpdatePartyRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartyLedgerEntryDto>> GetPartyLedgerAsync(
        Guid partyId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 500,
        CancellationToken cancellationToken = default);

    Task<RecordCustomerPaymentResult> RecordCustomerPaymentAsync(
        RecordCustomerPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordSupplierPaymentResult> RecordSupplierPaymentAsync(
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesInvoiceSummaryDto>> GetInvoicesAsync(
        Guid? shiftId = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CashierSalesSummaryDto>> GetSalesByCashierAsync(
        DateTimeOffset from,
        DateTimeOffset to,
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

    Task<CreatePurchaseOrderResultDto> CreatePurchaseAsync(
        CreatePurchaseRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDetailDto> UpdatePurchaseAsync(
        Guid purchaseOrderId,
        UpdateOpenPurchaseRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ReceiveStockResultDto> ReceivePurchaseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseOrderSummaryDto>> GetPurchasesAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDetailDto> GetPurchaseAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<StoreStatusDto> GetStoreStatusAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentAccountDto>> ListPaymentAccountsAsync(
        CancellationToken cancellationToken = default);
}
