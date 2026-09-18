using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class SalesReturnService : ISalesReturnService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;

    public SalesReturnService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService ?? throw new ArgumentNullException(nameof(partyLedgerService));
    }

    public Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ArgumentNullException.ThrowIfNull(request);
            WebPosDbContext context = _ambient.Required;

            if (string.IsNullOrWhiteSpace(request.OriginalInvoiceNo))
            {
                throw new ArgumentException("Original invoice number is required.", nameof(request));
            }

            if (request.Items.Count == 0)
            {
                throw new ArgumentException("At least one return line is required.", nameof(request));
            }

            SalesInvoice invoice = await context.SalesInvoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceNo == request.OriginalInvoiceNo, ct)
                ?? throw new InvalidOperationException($"Invoice '{request.OriginalInvoiceNo}' was not found.");

            CashierShift shift = await context.CashierShifts
                .FirstOrDefaultAsync(s => s.Id == invoice.ShiftId, ct)
                ?? throw new InvalidOperationException("The original invoice shift was not found.");

            List<SalesReturnItem> priorReturnItems = await (
                from ri in context.SalesReturnItems
                join sr in context.SalesReturns on ri.SalesReturnId equals sr.Id
                where sr.OriginalInvoiceNo == request.OriginalInvoiceNo
                select ri).ToListAsync(ct);

            long refundGrossPaisa = 0;
            List<(ReturnLineRequest Line, SalesItem SoldItem, decimal RestoreQty, Guid StockProductId)> resolved = [];

            foreach (ReturnLineRequest line in request.Items)
            {
                if (line.Quantity <= 0)
                {
                    throw new ArgumentException("Return quantity must be greater than zero.");
                }

                SalesItem? soldItem = invoice.Items.FirstOrDefault(i =>
                    i.ProductId == line.ProductId
                    && (line.BatchId is null
                        || i.BatchId is null
                        || i.BatchId == line.BatchId));

                if (soldItem is null)
                {
                    throw new InvalidOperationException(
                        $"Product was not found on invoice '{request.OriginalInvoiceNo}'.");
                }

                decimal alreadyReturned = priorReturnItems
                    .Where(r =>
                        r.ProductId == line.ProductId
                        && (line.BatchId is null
                            || r.BatchId is null
                            || r.BatchId == line.BatchId))
                    .Sum(r => r.Quantity);

                decimal returnable = soldItem.Quantity - alreadyReturned;
                if (line.Quantity > returnable)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.Quantity}; only {returnable} remaining for this line.");
                }

                decimal restoreQty = soldItem.ParentQtyDeducted > 0
                    ? soldItem.ParentQtyDeducted * (line.Quantity / soldItem.Quantity)
                    : line.Quantity;

                Guid stockProductId = soldItem.ProductId;
                if (soldItem.ParentQtyDeducted > 0)
                {
                    Product product = await context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == soldItem.ProductId, ct)
                        ?? throw new InvalidOperationException("Sold product was not found.");
                    stockProductId = product.ParentProductId
                        ?? throw new InvalidOperationException(
                            "Alias sale is missing ParentProductId for stock restore.");
                }

                long lineGross = (long)Math.Round(
                    line.Quantity * soldItem.UnitPricePaisa,
                    MidpointRounding.AwayFromZero);
                refundGrossPaisa += lineGross;
                resolved.Add((line, soldItem, restoreQty, stockProductId));
            }

            long originalGrossPaisa = invoice.Items.Sum(i =>
                (long)Math.Round(i.Quantity * i.UnitPricePaisa, MidpointRounding.AwayFromZero));

            if (originalGrossPaisa <= 0)
            {
                throw new InvalidOperationException("Original invoice has no gross amount to reverse.");
            }

            long refundDiscountPaisa = (long)Math.Round(
                (decimal)invoice.DiscountAmountPaisa * refundGrossPaisa / originalGrossPaisa,
                MidpointRounding.AwayFromZero);
            long refundNetPaisa = refundGrossPaisa - refundDiscountPaisa;
            if (refundNetPaisa <= 0)
            {
                throw new InvalidOperationException("Refund net amount must be greater than zero.");
            }

            List<GeneralLedgerEntry> originalLedger = await context.GeneralLedgerEntries
                .Where(e => e.ReferenceNo == invoice.ReceiptNumber && e.TransactionType == "SALE")
                .ToListAsync(ct);

            if (originalLedger.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No SALE ledger entries found for invoice '{request.OriginalInvoiceNo}'.");
            }

            Guid originalGroupId = originalLedger[0].TransactionGroupId;
            string paymentAccount = originalLedger
                .Where(e =>
                    e.DebitPaisa > 0
                    && !e.AccountCode.StartsWith("EXPENSE:", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(e.AccountCode, LedgerAccounts.Revenue, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.AccountCode)
                .FirstOrDefault()
                ?? LedgerAccounts.PaymentMethodToAccount(invoice.PaymentMethod);

            List<LedgerPosting> reversalPostings =
            [
                new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Revenue,
                    DebitPaisa = refundGrossPaisa,
                    CreditPaisa = 0
                },
                new LedgerPosting
                {
                    AccountCode = paymentAccount,
                    DebitPaisa = 0,
                    CreditPaisa = refundNetPaisa
                }
            ];

            if (refundDiscountPaisa > 0)
            {
                reversalPostings.Add(new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Expense(ExpenseCategories.Discount),
                    DebitPaisa = 0,
                    CreditPaisa = refundDiscountPaisa
                });
            }

            Guid salesReturnId = Guid.NewGuid();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            SalesReturn salesReturn = new()
            {
                Id = salesReturnId,
                OriginalInvoiceNo = invoice.InvoiceNo,
                CashierId = request.CashierId,
                CustomerId = invoice.CustomerId,
                TotalRefundPaisa = refundNetPaisa,
                CreatedAt = now
            };
            context.SalesReturns.Add(salesReturn);

            foreach ((ReturnLineRequest line, SalesItem soldItem, decimal restoreQty, Guid stockProductId) in resolved)
            {
                context.SalesReturnItems.Add(new SalesReturnItem
                {
                    Id = Guid.NewGuid(),
                    SalesReturnId = salesReturnId,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    Quantity = line.Quantity,
                    RefundUnitPricePaisa = soldItem.UnitPricePaisa,
                    ReturnCondition = string.IsNullOrWhiteSpace(line.ReturnCondition)
                        ? "GOOD"
                        : line.ReturnCondition.ToUpperInvariant()
                });

                Product? stockProduct = await context.Products
                    .FirstOrDefaultAsync(p => p.Id == stockProductId, ct)
                    ?? throw new InvalidOperationException("Stock product was not found for return.");
                stockProduct.StockQty += restoreQty;
                stockProduct.UpdatedAt = now;
            }

            await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "REVERSAL",
                    ReferenceNo = invoice.ReceiptNumber,
                    ReferenceDetails =
                        $"Sales return reversing group {originalGroupId} for invoice {invoice.InvoiceNo}",
                    ShiftId = invoice.ShiftId,
                    PartyId = invoice.CustomerId,
                    TransactionGroupId = originalGroupId,
                    Postings = reversalPostings
                },
                ct);

            if (invoice.CustomerId is Guid partyId)
            {
                await _partyLedgerService.RecordPartyTransactionAsync(
                    partyId,
                    refundNetPaisa,
                    "CUSTOMER_RETURN",
                    invoice.PaymentMethod,
                    invoice.InvoiceNo,
                    ct);
            }

            if (LedgerAccounts.IsCashAccount(paymentAccount))
            {
                shift.ExpectedCashPaisa -= refundNetPaisa;
            }

            return new ReturnItemsResult
            {
                SalesReturnId = salesReturnId,
                OriginalInvoiceNo = invoice.InvoiceNo,
                TotalRefundPaisa = refundNetPaisa,
                TransactionGroupId = originalGroupId
            };
        }, cancellationToken);
}
