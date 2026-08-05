using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class PurchaseReturnService : IPurchaseReturnService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly ITenantService _tenantService;

    public PurchaseReturnService(
        WebPosDbContext context,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        ITenantService tenantService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService
            ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<CreatePurchaseReturnResult> CreatePurchaseReturnAsync(
        CreatePurchaseReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidateRequest(request);

            PurchaseOrder purchaseOrder = await _context.PurchaseOrders
                .Include(order => order.Items)
                .FirstOrDefaultAsync(
                    order =>
                        order.Id == request.OriginalPurchaseOrderId
                        && order.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new InvalidOperationException(
                    "Purchase order was not found for the current tenant.");

            bool managerExists = await _context.Users.AnyAsync(
                user =>
                    user.Id == request.ManagerId
                    && user.TenantId == _tenantService.TenantId
                    && user.IsActive,
                ct);

            if (!managerExists)
            {
                throw new InvalidOperationException(
                    "Manager was not found or is inactive for the current tenant.");
            }

            List<(PurchaseReturnLineRequest Line, ProductBatch Batch, long LineCreditPaisa)>
                resolved = [];
            long totalCreditDeductionPaisa = 0;

            List<PurchaseReturnItem> priorReturnItems = await (
                from item in _context.PurchaseReturnItems
                join existingReturn in _context.PurchaseReturns
                    on item.PurchaseReturnId equals existingReturn.Id
                where existingReturn.OriginalPurchaseOrderId == request.OriginalPurchaseOrderId
                      && existingReturn.TenantId == _tenantService.TenantId
                select item).ToListAsync(ct);

            foreach (PurchaseReturnLineRequest line in request.Lines)
            {
                if (line.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Return quantity must be greater than zero.");
                }

                ProductBatch batch = await _context.ProductBatches
                    .FirstOrDefaultAsync(
                        candidate =>
                            candidate.Id == line.BatchId
                            && candidate.TenantId == _tenantService.TenantId,
                        ct)
                    ?? throw new InvalidOperationException(
                        $"Batch {line.BatchId} was not found for the current tenant.");

                if (batch.ProductId != line.ProductId)
                {
                    throw new InvalidOperationException(
                        $"Batch {line.BatchId} does not belong to product {line.ProductId}.");
                }

                if (batch.PurchaseOrderId is Guid batchPurchaseOrderId
                    && batchPurchaseOrderId != purchaseOrder.Id)
                {
                    throw new InvalidOperationException(
                        $"Batch {line.BatchId} is not linked to the original purchase order.");
                }

                if (line.Quantity > batch.CurrentQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient batch stock to return. Available: {batch.CurrentQty}.");
                }

                decimal purchasedQty = purchaseOrder.Items
                    .Where(item =>
                        item.ProductId == line.ProductId
                        && (item.BatchId is null || item.BatchId == line.BatchId))
                    .Sum(item => item.QuantityReceived + item.BonusQuantity);

                decimal alreadyReturned = priorReturnItems
                    .Where(item =>
                        item.ProductId == line.ProductId
                        && item.BatchId == line.BatchId)
                    .Sum(item => item.Quantity);

                decimal returnable = purchasedQty - alreadyReturned;
                if (purchasedQty > 0 && line.Quantity > returnable)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.Quantity}; only {returnable} remaining for this purchase line.");
                }

                long lineCreditPaisa = (long)Math.Round(
                    line.Quantity * batch.CostPricePaisa,
                    MidpointRounding.AwayFromZero);
                totalCreditDeductionPaisa += lineCreditPaisa;
                resolved.Add((line, batch, lineCreditPaisa));
            }

            if (totalCreditDeductionPaisa <= 0)
            {
                throw new InvalidOperationException(
                    "Purchase return credit must be greater than zero.");
            }

            Guid purchaseReturnId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            PurchaseReturn purchaseReturn = new()
            {
                Id = purchaseReturnId,
                TenantId = _tenantService.TenantId,
                OriginalPurchaseOrderId = purchaseOrder.Id,
                ManagerId = request.ManagerId,
                SupplierId = purchaseOrder.SupplierId,
                TotalCreditDeductionPaisa = totalCreditDeductionPaisa,
                CreatedAt = now
            };
            _context.PurchaseReturns.Add(purchaseReturn);

            foreach ((PurchaseReturnLineRequest line, ProductBatch batch, _) in resolved)
            {
                _context.PurchaseReturnItems.Add(new PurchaseReturnItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = _tenantService.TenantId,
                    PurchaseReturnId = purchaseReturnId,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    Quantity = line.Quantity,
                    CostPerUnitPaisa = batch.CostPricePaisa
                });

                batch.CurrentQty -= line.Quantity;
            }

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "PURCHASE_RETURN",
                    ReferenceNo = purchaseReturnId.ToString(),
                    ReferenceDetails =
                        $"Purchase return against PO {purchaseOrder.Id}",
                    PartyId = purchaseOrder.SupplierId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.AccountsPayable,
                            DebitPaisa = totalCreditDeductionPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Inventory,
                            DebitPaisa = 0,
                            CreditPaisa = totalCreditDeductionPaisa
                        }
                    ]
                },
                ct);

            await _partyLedgerService.RecordPartyTransactionAsync(
                purchaseOrder.SupplierId,
                totalCreditDeductionPaisa,
                "SUPPLIER_RETURN",
                "CREDIT",
                purchaseOrder.Id.ToString(),
                ct);

            return new CreatePurchaseReturnResult
            {
                PurchaseReturnId = purchaseReturnId,
                TotalCreditDeductionPaisa = totalCreditDeductionPaisa,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for purchase returns.");
        }
    }

    private static void ValidateRequest(CreatePurchaseReturnRequest request)
    {
        if (request.OriginalPurchaseOrderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Original purchase order is required.",
                nameof(request));
        }

        if (request.ManagerId == Guid.Empty)
        {
            throw new ArgumentException("Manager is required.", nameof(request));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException(
                "At least one return line is required.",
                nameof(request));
        }
    }
}
