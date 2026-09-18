using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class SalesService : ISalesService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly ICashAccountService _cashAccountService;

    public SalesService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        ICashAccountService cashAccountService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _cashAccountService = cashAccountService
            ?? throw new ArgumentNullException(nameof(cashAccountService));
    }

    public Task<CompleteSaleResult> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidateRequest(request);
            WebPosDbContext context = _ambient.Required;

            long grossPaisa = request.Lines.Sum(l =>
                (long)Math.Round(l.Quantity * l.UnitPricePaisa, MidpointRounding.AwayFromZero));

            if (request.DiscountAmountPaisa > grossPaisa)
            {
                throw new InvalidOperationException("Discount cannot exceed the gross sale amount.");
            }

            long netPaisa = grossPaisa - request.DiscountAmountPaisa;
            if (netPaisa <= 0)
            {
                throw new InvalidOperationException("Net sale amount must be greater than zero.");
            }

            CashierShift? shift = request.TerminalId is Guid terminalId
                ? await context.CashierShifts.FirstOrDefaultAsync(candidate =>
                    candidate.Id == request.ShiftId
                    && candidate.Status == "OPEN"
                    && candidate.TerminalId == terminalId
                    && candidate.CashierId == request.CashierId,
                    ct)
                : null;

            if (shift is null)
            {
                throw new ShiftAuthorizationException(
                    "The shift is inactive or is not authorized for this terminal and cashier.");
            }

            if (string.Equals(request.PaymentMethod, "CREDIT", StringComparison.OrdinalIgnoreCase))
            {
                if (request.CustomerId is null)
                {
                    throw new InvalidOperationException("CREDIT sales require a customer.");
                }

                Party? customer = await context.Parties
                    .FirstOrDefaultAsync(p => p.Id == request.CustomerId.Value, ct);

                if (customer is null || !string.Equals(customer.PartyType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CREDIT sales require a valid customer party profile.");
                }
            }

            List<Guid> productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
            Dictionary<Guid, Product> products = await context.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .ToDictionaryAsync(p => p.Id, ct);

            List<Guid> parentIds = products.Values
                .Where(p => p.ParentProductId is Guid)
                .Select(p => p.ParentProductId!.Value)
                .Distinct()
                .ToList();
            Dictionary<Guid, Product> parents = parentIds.Count == 0
                ? []
                : await context.Products
                    .Where(p => parentIds.Contains(p.Id) && !p.IsDeleted)
                    .ToDictionaryAsync(p => p.Id, ct);

            foreach (SaleLineRequest line in request.Lines)
            {
                if (!products.TryGetValue(line.ProductId, out Product? product))
                {
                    throw new InvalidOperationException($"Product {line.ProductName} was not found.");
                }

                if (line.UnitPricePaisa != product.RetailPricePaisa)
                {
                    throw new InvalidOperationException(
                        $"The price for {line.ProductName} has changed. Refresh the product list and try again.");
                }

                if (product.ParentProductId is Guid parentId)
                {
                    if (product.DeductionMultiplier is not decimal mult || mult <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Alias {line.ProductName} is missing a valid DeductionMultiplier.");
                    }

                    if (!parents.TryGetValue(parentId, out Product? parent))
                    {
                        throw new InvalidOperationException(
                            $"Parent product for {line.ProductName} was not found.");
                    }

                    decimal parentNeeded = line.Quantity * mult;
                    if (parent.StockQty < parentNeeded)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient bulk stock for {line.ProductName}. " +
                            $"Available: {parent.StockQty:0.###} {parent.BaseUnit}, " +
                            $"needed: {parentNeeded:0.###} {parent.BaseUnit}.");
                    }
                }
                else if (product.StockQty < line.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for {line.ProductName}. " +
                        $"Available: {product.StockQty:0.###}, requested: {line.Quantity}.");
                }
            }

            string receiptNumber = request.InvoiceNo;
            DateTimeOffset now = DateTimeOffset.UtcNow;

            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            SalesInvoice invoice = new()
            {
                InvoiceNo = request.InvoiceNo,
                ShiftId = request.ShiftId,
                TerminalId = request.TerminalId,
                CashierId = request.CashierId,
                CustomerId = request.CustomerId,
                TotalAmountPaisa = netPaisa,
                TaxAmountPaisa = 0,
                DiscountAmountPaisa = request.DiscountAmountPaisa,
                DiscountReason = request.DiscountReason,
                ReceiptNumber = receiptNumber,
                PaymentMethod = paymentMethod,
                AmountPaidPaisa = paymentMethod == "CREDIT" ? 0 : netPaisa,
                CreatedAt = now
            };
            context.SalesInvoices.Add(invoice);

            foreach (SaleLineRequest line in request.Lines)
            {
                Product product = products[line.ProductId];
                long unitCostPaisa;
                decimal parentQtyDeducted = 0m;

                if (product.ParentProductId is Guid parentId)
                {
                    Product parent = parents[parentId];
                    decimal mult = product.DeductionMultiplier!.Value;
                    decimal parentQty = line.Quantity * mult;
                    if (parent.StockQty < parentQty)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient parent stock for {line.ProductName}.");
                    }

                    parent.StockQty -= parentQty;
                    parent.UpdatedAt = now;
                    unitCostPaisa = (long)Math.Round(
                        parent.CostPricePaisa * mult,
                        MidpointRounding.AwayFromZero);
                    parentQtyDeducted = parentQty;
                }
                else
                {
                    if (product.StockQty < line.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for {line.ProductName}.");
                    }

                    product.StockQty -= line.Quantity;
                    product.UpdatedAt = now;
                    unitCostPaisa = product.CostPricePaisa;
                }

                context.SalesItems.Add(new SalesItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceNo = request.InvoiceNo,
                    ProductId = line.ProductId,
                    BatchId = null,
                    Quantity = line.Quantity,
                    UnitPricePaisa = line.UnitPricePaisa,
                    UnitCostPaisa = unitCostPaisa,
                    DiscountAppliedPaisa = line.DiscountAppliedPaisa,
                    ParentQtyDeducted = parentQtyDeducted
                });
            }

            string paymentAccount;
            bool affectsTill;
            if (string.Equals(paymentMethod, "CASH", StringComparison.OrdinalIgnoreCase))
            {
                CashPaymentResolution till = await _cashAccountService.ResolveTillAccountForTerminalAsync(
                    shift.TerminalId,
                    cancellationToken: ct);
                paymentAccount = till.AccountCode;
                affectsTill = till.AffectsTillDrawer;
            }
            else
            {
                CashPaymentResolution payment = await _cashAccountService.ResolvePaymentAccountAsync(
                    cashAccountId: null,
                    paymentMethod,
                    ct);
                paymentAccount = payment.AccountCode;
                affectsTill = payment.AffectsTillDrawer;
            }

            List<LedgerPosting> postings =
            [
                new LedgerPosting
                {
                    AccountCode = paymentAccount,
                    DebitPaisa = netPaisa,
                    CreditPaisa = 0
                },
                new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Revenue,
                    DebitPaisa = 0,
                    CreditPaisa = grossPaisa
                }
            ];

            if (request.DiscountAmountPaisa > 0)
            {
                postings.Add(new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Expense(ExpenseCategories.Discount),
                    DebitPaisa = request.DiscountAmountPaisa,
                    CreditPaisa = 0
                });
            }

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "SALE",
                    ReferenceNo = receiptNumber,
                    ReferenceDetails = $"Sale invoice {request.InvoiceNo}",
                    ShiftId = request.ShiftId,
                    PartyId = request.CustomerId,
                    Postings = postings
                },
                ct);

            if (request.CustomerId is Guid partyId)
            {
                await _partyLedgerService.RecordPartyTransactionAsync(
                    partyId,
                    netPaisa,
                    "SALE",
                    request.PaymentMethod,
                    request.InvoiceNo,
                    ct);
            }

            if (affectsTill)
            {
                shift.ExpectedCashPaisa += netPaisa;
            }

            return new CompleteSaleResult
            {
                InvoiceNo = request.InvoiceNo,
                ReceiptNumber = receiptNumber,
                TotalAmountPaisa = netPaisa,
                GrossAmountPaisa = grossPaisa,
                DiscountAmountPaisa = request.DiscountAmountPaisa,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);

    private static void ValidateRequest(CompleteSaleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.InvoiceNo))
        {
            throw new ArgumentException("Invoice number is required.", nameof(request));
        }

        if (request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one sale line is required.", nameof(request));
        }

        foreach (SaleLineRequest line in request.Lines)
        {
            if (line.Quantity <= 0)
            {
                throw new ArgumentException($"Quantity must be greater than zero for {line.ProductName}.");
            }

            if (line.UnitPricePaisa <= 0)
            {
                throw new ArgumentException($"Unit price must be greater than zero for {line.ProductName}.");
            }
        }
    }
}
