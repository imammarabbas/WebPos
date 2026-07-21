using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class PurchaseService : IPurchaseService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly IPartyService _partyService;
    private readonly ITenantService _tenantService;
    private readonly IValidator<CreatePurchaseRequest> _createValidator;

    public PurchaseService(
        WebPosDbContext context,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        IPartyService partyService,
        ITenantService tenantService,
        IValidator<CreatePurchaseRequest> createValidator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService
            ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _partyService = partyService
            ?? throw new ArgumentNullException(nameof(partyService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
        _createValidator = createValidator
            ?? throw new ArgumentNullException(nameof(createValidator));
    }

    public Task<CreatePurchaseOrderResult> CreatePurchaseOrderAsync(
        CreatePurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidationResult validation =
                await _createValidator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                throw new ValidationException(validation.Errors);
            }

            // Hard supplier check via PartyService (tenant + PartyType.SUPPLIER).
            await _partyService.GetSupplierAsync(request.SupplierId, ct);

            long subTotalPaisa = 0;
            foreach (CreatePurchaseLineRequest line in request.Lines)
            {
                checked
                {
                    subTotalPaisa += (long)Math.Round(
                        line.Quantity * line.PurchasePricePaisa,
                        MidpointRounding.AwayFromZero);
                }
            }

            if (request.DiscountPaisa > subTotalPaisa)
            {
                throw new InvalidOperationException(
                    "Discount cannot exceed the purchase subtotal.");
            }

            long netPayablePaisa = subTotalPaisa - request.DiscountPaisa;
            if (netPayablePaisa <= 0)
            {
                throw new InvalidOperationException(
                    "Net payable must be greater than zero.");
            }

            Guid purchaseOrderId = Guid.NewGuid();
            Guid tenantId = _tenantService.TenantId;
            DateTimeOffset purchaseDate = request.PurchaseDate == default
                ? DateTimeOffset.UtcNow
                : request.PurchaseDate;

            PurchaseOrder order = new()
            {
                Id = purchaseOrderId,
                TenantId = tenantId,
                SupplierInvoiceNo = request.SupplierInvoiceNo.Trim(),
                SupplierId = request.SupplierId,
                ReceiverId = request.ReceiverId,
                SubTotalPaisa = subTotalPaisa,
                DiscountPaisa = request.DiscountPaisa,
                NetPayablePaisa = netPayablePaisa,
                PaymentStatus = "PENDING",
                IsReceived = false,
                CreatedAt = purchaseDate
            };
            _context.PurchaseOrders.Add(order);

            foreach (CreatePurchaseLineRequest line in request.Lines)
            {
                _context.PurchaseItems.Add(new PurchaseItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PurchaseOrderId = purchaseOrderId,
                    ProductId = line.ProductId,
                    QuantityReceived = line.Quantity,
                    BonusQuantity = line.BonusQuantity,
                    CostPricePerUnitPaisa = line.PurchasePricePaisa,
                    RetailPricePerUnitPaisa = line.RetailPricePaisa,
                    BatchNumber = line.BatchNumber?.Trim() ?? string.Empty,
                    ExpiryDate = line.ExpiryDate,
                    RackLocation = string.IsNullOrWhiteSpace(line.RackLocation)
                        ? null
                        : line.RackLocation.Trim(),
                    BatchId = null
                });
            }

            return new CreatePurchaseOrderResult
            {
                PurchaseOrderId = purchaseOrderId,
                SubTotalPaisa = subTotalPaisa,
                DiscountPaisa = request.DiscountPaisa,
                NetPayablePaisa = netPayablePaisa,
                IsReceived = false
            };
        }, cancellationToken);
    }

    public Task<ReceiveStockResult> ReceiveStockAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Purchase order id is required.",
                nameof(purchaseOrderId));
        }

        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;

            PurchaseOrder order = await _context.PurchaseOrders
                .Include(candidate => candidate.Items)
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == purchaseOrderId
                        && candidate.TenantId == tenantId,
                    ct)
                ?? throw new InvalidOperationException(
                    "Purchase order was not found for the current tenant.");

            if (order.IsReceived)
            {
                throw new InvalidOperationException("Order already processed.");
            }

            await _partyService.GetSupplierAsync(order.SupplierId, ct);

            if (order.Items.Count == 0)
            {
                throw new InvalidOperationException(
                    "Purchase order has no items to receive.");
            }

            List<Guid> batchIds = [];
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (PurchaseItem item in order.Items.OrderBy(i => i.Id))
            {
                decimal stockQty = item.QuantityReceived + item.BonusQuantity;
                if (stockQty <= 0)
                {
                    throw new InvalidOperationException(
                        $"Purchase item {item.Id} has non-positive receive quantity.");
                }

                string batchNumber = string.IsNullOrWhiteSpace(item.BatchNumber)
                    ? BuildDefaultBatchNumber(order, item)
                    : item.BatchNumber.Trim();

                ProductBatch batch = new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ProductId = item.ProductId,
                    BatchNumber = batchNumber,
                    ExpiryDate = item.ExpiryDate,
                    PurchasePricePaisa = item.CostPricePerUnitPaisa,
                    RetailPricePaisa = item.RetailPricePerUnitPaisa,
                    InitialQty = stockQty,
                    CurrentQty = stockQty,
                    SupplierId = order.SupplierId,
                    RackLocation = item.RackLocation,
                    PurchaseOrderId = order.Id,
                    CreatedAt = now
                };
                _context.ProductBatches.Add(batch);
                item.BatchId = batch.Id;
                batchIds.Add(batch.Id);
            }

            long inventoryDebitPaisa = order.NetPayablePaisa;

            // Inventory + AP posting in the same ambient transaction as batch updates.
            // If PostBalancedEntriesAsync fails, ExecuteInTransactionAsync rolls back stock.
            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "PURCHASE",
                    ReferenceNo = order.Id.ToString(),
                    ReferenceDetails =
                        $"Purchase receive invoice {order.SupplierInvoiceNo}",
                    PartyId = order.SupplierId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Inventory,
                            DebitPaisa = inventoryDebitPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.AccountsPayable,
                            DebitPaisa = 0,
                            CreditPaisa = inventoryDebitPaisa
                        }
                    ]
                },
                ct);

            await _partyLedgerService.RecordPartyTransactionAsync(
                order.SupplierId,
                inventoryDebitPaisa,
                "PURCHASE",
                "CREDIT",
                order.Id.ToString(),
                ct);

            order.IsReceived = true;
            order.PaymentStatus = "CREDIT";

            return new ReceiveStockResult
            {
                PurchaseOrderId = order.Id,
                NetPayablePaisa = order.NetPayablePaisa,
                TransactionGroupId = transactionGroupId,
                BatchIds = batchIds
            };
        }, cancellationToken);
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required for purchase operations.");
        }
    }

    private static string BuildDefaultBatchNumber(PurchaseOrder order, PurchaseItem item)
    {
        string raw = $"PO-{order.SupplierInvoiceNo}-{item.ProductId:N}";
        return raw.Length <= 100 ? raw : raw[..100];
    }
}
