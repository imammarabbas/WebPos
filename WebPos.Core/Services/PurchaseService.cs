using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using WebPos.Core;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Security;

namespace WebPos.Core.Services;

public sealed class PurchaseService : IPurchaseService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly IPartyService _partyService;
    private readonly IProductAdminService _productAdminService;
    private readonly ITenantService _tenantService;
    private readonly LoginService _loginService;
    private readonly IValidator<CreatePurchaseRequest> _createValidator;

    public PurchaseService(
        WebPosDbContext context,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        IPartyService partyService,
        IProductAdminService productAdminService,
        ITenantService tenantService,
        LoginService loginService,
        IValidator<CreatePurchaseRequest> createValidator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService
            ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _partyService = partyService
            ?? throw new ArgumentNullException(nameof(partyService));
        _productAdminService = productAdminService
            ?? throw new ArgumentNullException(nameof(productAdminService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
        _loginService = loginService
            ?? throw new ArgumentNullException(nameof(loginService));
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
            List<Guid> productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
            Dictionary<Guid, Product> products = await _context.Products
                .IgnoreQueryFilters()
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, ct);

            foreach (PurchaseItem item in order.Items.OrderBy(i => i.Id))
            {
                int multiplier = 1;
                if (products.TryGetValue(item.ProductId, out Product? product))
                {
                    multiplier = UnitConversion.NormalizeMultiplier(product.ConversionMultiplier);
                }

                decimal purchaseQty = item.QuantityReceived + item.BonusQuantity;
                decimal stockQty = UnitConversion.ToStockQty(purchaseQty, multiplier);
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
                    PurchasePricePaisa = UnitConversion.ToStockUnitPricePaisa(
                        item.CostPricePerUnitPaisa,
                        multiplier),
                    RetailPricePaisa = UnitConversion.ToStockUnitPricePaisa(
                        item.RetailPricePerUnitPaisa,
                        multiplier),
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

    public Task<ReceiveStockResult> QuickReceiveAsync(
        QuickReceiveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.ReceiverId == Guid.Empty)
        {
            throw new InvalidOperationException("Receiver id is required.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one receive line is required.");
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            bool creatingSupplier = request.NewSupplier is not null;
            bool creatingProduct = request.Lines.Any(line => line.NewProduct is not null);
            bool priceChanged = false;

            foreach (QuickReceiveLineRequest line in request.Lines)
            {
                if (line.ProductId is Guid productId && productId != Guid.Empty && line.NewProduct is null)
                {
                    ProductBatch? lastBatch = await _context.ProductBatches
                        .AsNoTracking()
                        .Where(b => b.ProductId == productId)
                        .OrderByDescending(b => b.CreatedAt)
                        .FirstOrDefaultAsync(ct);

                    if (lastBatch is not null
                        && (lastBatch.CostPricePaisa != line.PurchasePricePaisa
                            || lastBatch.RetailPricePaisa != line.RetailPricePaisa))
                    {
                        priceChanged = true;
                        break;
                    }
                }
            }

            bool requiresElevation = creatingSupplier || creatingProduct || priceChanged;
            if (requiresElevation)
            {
                bool elevated = await _loginService.VerifyManagerPinAsync(
                    request.ManagerPin ?? string.Empty,
                    ct);
                if (!elevated)
                {
                    throw new UnauthorizedAccessException(
                        "Manager PIN is required to create products/suppliers or change cost/retail.");
                }
            }

            Guid supplierId;
            if (creatingSupplier)
            {
                QuickReceiveNewSupplierRequest newSupplier = request.NewSupplier!;
                PartyDto createdSupplier = await _partyService.CreatePartyAsync(
                    new CreatePartyRequest
                    {
                        Role = PartyTypes.Supplier,
                        Name = newSupplier.Name,
                        PhoneNumber = newSupplier.PhoneNumber,
                        Address = newSupplier.Address
                    },
                    ct);
                supplierId = createdSupplier.Id;
            }
            else
            {
                if (request.SupplierId is not Guid sid || sid == Guid.Empty)
                {
                    throw new InvalidOperationException("Supplier id is required.");
                }

                await _partyService.GetSupplierAsync(sid, ct);
                supplierId = sid;
            }

            List<CreatePurchaseLineRequest> createLines = [];
            foreach (QuickReceiveLineRequest line in request.Lines)
            {
                Guid productId;
                if (line.NewProduct is not null)
                {
                    QuickReceiveNewProductRequest np = line.NewProduct;
                    string sku = string.IsNullOrWhiteSpace(np.Sku)
                        ? $"SKU-{Guid.NewGuid():N}"[..16]
                        : np.Sku.Trim();
                    ProductAdminDto createdProduct = await _productAdminService.CreateAsync(
                        new UpsertProductRequest
                        {
                            Name = np.Name.Trim(),
                            Sku = sku,
                            Barcode = np.Barcode?.Trim() ?? string.Empty,
                            ShortCode = np.ShortCode?.Trim() ?? string.Empty,
                            IsLoose = np.IsLoose,
                            Brand = np.Brand?.Trim() ?? string.Empty,
                            BaseUnit = string.IsNullOrWhiteSpace(np.BaseUnit) ? "PCS" : np.BaseUnit.Trim()
                        },
                        ct);
                    productId = createdProduct.Id;
                }
                else
                {
                    if (line.ProductId is not Guid pid || pid == Guid.Empty)
                    {
                        throw new InvalidOperationException(
                            "Each line requires ProductId or NewProduct.");
                    }

                    productId = pid;
                }

                createLines.Add(new CreatePurchaseLineRequest
                {
                    ProductId = productId,
                    Quantity = line.Quantity,
                    BonusQuantity = line.BonusQuantity,
                    PurchasePricePaisa = line.PurchasePricePaisa,
                    RetailPricePaisa = line.RetailPricePaisa,
                    BatchNumber = line.BatchNumber,
                    ExpiryDate = line.ExpiryDate,
                    RackLocation = line.RackLocation
                });
            }

            // Persist inline creates so purchase validators can resolve product FKs.
            await _context.SaveChangesAsync(ct);

            // One ad-hoc PO for every line in this receive (header + multi-line intake).
            CreatePurchaseOrderResult created = await CreatePurchaseOrderAsync(
                new CreatePurchaseRequest
                {
                    SupplierId = supplierId,
                    ReceiverId = request.ReceiverId,
                    SupplierInvoiceNo = request.SupplierInvoiceNo,
                    PurchaseDate = request.PurchaseDate == default
                        ? DateTimeOffset.UtcNow
                        : request.PurchaseDate,
                    DiscountPaisa = request.DiscountPaisa,
                    Lines = createLines
                },
                ct);

            // CreatePurchaseOrderAsync is nested in this ambient transaction, so it does not
            // SaveChanges. Flush before ReceiveStockAsync queries the same PO by id.
            await _context.SaveChangesAsync(ct);

            return await ReceiveStockAsync(created.PurchaseOrderId, ct);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        int take = Math.Clamp(limit, 1, 200);

        List<PurchaseOrder> orders = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return orders.Select(o => new PurchaseOrderSummaryDto
        {
            Id = o.Id,
            SupplierInvoiceNo = o.SupplierInvoiceNo,
            SupplierId = o.SupplierId,
            SupplierName = o.Supplier?.Name,
            NetPayablePaisa = o.NetPayablePaisa,
            IsReceived = o.IsReceived,
            PaymentStatus = o.PaymentStatus,
            CreatedAt = o.CreatedAt,
            LineCount = o.Items.Count
        }).ToList();
    }

    public async Task<PurchaseOrderDetailDto?> GetAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        PurchaseOrder? order = await _context.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        return new PurchaseOrderDetailDto
        {
            Id = order.Id,
            SupplierInvoiceNo = order.SupplierInvoiceNo,
            SupplierId = order.SupplierId,
            SupplierName = order.Supplier?.Name,
            ReceiverId = order.ReceiverId,
            SubTotalPaisa = order.SubTotalPaisa,
            DiscountPaisa = order.DiscountPaisa,
            NetPayablePaisa = order.NetPayablePaisa,
            IsReceived = order.IsReceived,
            PaymentStatus = order.PaymentStatus,
            CreatedAt = order.CreatedAt,
            Lines = order.Items.Select(i => new PurchaseOrderLineDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.Product?.Name,
                Quantity = i.QuantityReceived,
                BonusQuantity = i.BonusQuantity,
                PurchasePricePaisa = i.CostPricePerUnitPaisa,
                RetailPricePaisa = i.RetailPricePerUnitPaisa,
                BatchNumber = i.BatchNumber,
                ExpiryDate = i.ExpiryDate,
                RackLocation = i.RackLocation
            }).ToList()
        };
    }

    public async Task<PurchaseOrderDetailDto> UpdateOpenPurchaseAsync(
        Guid purchaseOrderId,
        UpdateOpenPurchaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one purchase line is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SupplierInvoiceNo))
        {
            throw new InvalidOperationException("Supplier invoice number is required.");
        }

        PurchaseOrder order = await _context.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId, cancellationToken)
            ?? throw new InvalidOperationException("Purchase order not found.");

        if (order.IsReceived)
        {
            throw new InvalidOperationException("Received purchase orders cannot be edited.");
        }

        if (request.SupplierId is Guid supplierId && supplierId != Guid.Empty)
        {
            await _partyService.GetSupplierAsync(supplierId, cancellationToken);
            order.SupplierId = supplierId;
        }

        long subTotalPaisa = 0;
        foreach (CreatePurchaseLineRequest line in request.Lines)
        {
            if (line.ProductId == Guid.Empty)
            {
                throw new InvalidOperationException("Each line must have a product.");
            }

            if (line.Quantity <= 0)
            {
                throw new InvalidOperationException("Line quantity must be greater than zero.");
            }

            if (line.PurchasePricePaisa <= 0 || line.RetailPricePaisa <= 0)
            {
                throw new InvalidOperationException("Cost and retail must be greater than zero.");
            }

            bool productExists = await _context.Products.AnyAsync(
                p => p.Id == line.ProductId && !p.IsDeleted,
                cancellationToken);
            if (!productExists)
            {
                throw new InvalidOperationException($"Product {line.ProductId} was not found.");
            }

            checked
            {
                subTotalPaisa += (long)Math.Round(
                    line.Quantity * line.PurchasePricePaisa,
                    MidpointRounding.AwayFromZero);
            }
        }

        if (request.DiscountPaisa > subTotalPaisa)
        {
            throw new InvalidOperationException("Discount cannot exceed the purchase subtotal.");
        }

        long netPayablePaisa = subTotalPaisa - request.DiscountPaisa;
        if (netPayablePaisa <= 0)
        {
            throw new InvalidOperationException("Net payable must be greater than zero.");
        }

        order.SupplierInvoiceNo = request.SupplierInvoiceNo.Trim();
        if (request.PurchaseDate is DateTimeOffset purchaseDate && purchaseDate != default)
        {
            order.CreatedAt = purchaseDate;
        }

        order.SubTotalPaisa = subTotalPaisa;
        order.DiscountPaisa = request.DiscountPaisa;
        order.NetPayablePaisa = netPayablePaisa;

        List<PurchaseItem> existingItems = order.Items.ToList();
        _context.PurchaseItems.RemoveRange(existingItems);
        order.Items.Clear();
        await _context.SaveChangesAsync(cancellationToken);

        Guid tenantId = _tenantService.TenantId;
        foreach (CreatePurchaseLineRequest line in request.Lines)
        {
            _context.PurchaseItems.Add(new PurchaseItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PurchaseOrderId = order.Id,
                ProductId = line.ProductId,
                QuantityReceived = line.Quantity,
                BonusQuantity = line.BonusQuantity,
                CostPricePerUnitPaisa = line.PurchasePricePaisa,
                RetailPricePerUnitPaisa = line.RetailPricePaisa,
                BatchNumber = string.IsNullOrWhiteSpace(line.BatchNumber)
                    ? string.Empty
                    : line.BatchNumber.Trim(),
                ExpiryDate = line.ExpiryDate,
                RackLocation = line.RackLocation
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return (await GetAsync(purchaseOrderId, cancellationToken))!;
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
