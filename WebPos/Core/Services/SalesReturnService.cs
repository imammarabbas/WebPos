using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class SalesReturnService : ISalesReturnService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly PartyLedgerService _partyLedgerService;

    public SalesReturnService(
        WebPosDbContext context,
        ITransactionService transactionService,
        PartyLedgerService partyLedgerService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService ?? throw new ArgumentNullException(nameof(partyLedgerService));
    }

    public Task<ReturnItemsResult> ReturnItemsAsync(
        ReturnItemsRequest request,
        CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.OriginalInvoiceNo))
            {
                throw new ArgumentException("Original invoice number is required.", nameof(request));
            }

            if (request.Items.Count == 0)
            {
                throw new ArgumentException("At least one return line is required.", nameof(request));
            }

            SalesInvoice invoice = await _context.SalesInvoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.InvoiceNo == request.OriginalInvoiceNo, ct)
                ?? throw new InvalidOperationException($"Invoice '{request.OriginalInvoiceNo}' was not found.");

            CashierShift shift = await _context.CashierShifts
                .FirstOrDefaultAsync(s => s.Id == invoice.ShiftId, ct)
                ?? throw new InvalidOperationException("The original invoice shift was not found.");

            List<SalesReturnItem> priorReturnItems = await (
                from ri in _context.SalesReturnItems
                join sr in _context.SalesReturns on ri.SalesReturnId equals sr.Id
                where sr.OriginalInvoiceNo == request.OriginalInvoiceNo
                select ri).ToListAsync(ct);

            long refundGrossPaisa = 0;
            List<(ReturnLineRequest Line, SalesItem SoldItem, ProductBatch Batch)> resolved = [];

            foreach (ReturnLineRequest line in request.Items)
            {
                if (line.Quantity <= 0)
                {
                    throw new ArgumentException("Return quantity must be greater than zero.");
                }

                SalesItem? soldItem = invoice.Items.FirstOrDefault(i =>
                    i.ProductId == line.ProductId && i.BatchId == line.BatchId);

                if (soldItem is null)
                {
                    throw new InvalidOperationException(
                        $"Product/batch was not found on invoice '{request.OriginalInvoiceNo}'.");
                }

                decimal alreadyReturned = priorReturnItems
                    .Where(r => r.ProductId == line.ProductId && r.BatchId == line.BatchId)
                    .Sum(r => r.Quantity);

                decimal returnable = soldItem.Quantity - alreadyReturned;
                if (line.Quantity > returnable)
                {
                    throw new InvalidOperationException(
                        $"Cannot return {line.Quantity}; only {returnable} remaining for this line.");
                }

                ProductBatch batch = await _context.ProductBatches
                    .FirstOrDefaultAsync(b => b.Id == line.BatchId, ct)
                    ?? throw new InvalidOperationException($"Batch {line.BatchId} was not found.");

                long lineGross = (long)Math.Round(
                    line.Quantity * soldItem.UnitPricePaisa,
                    MidpointRounding.AwayFromZero);
                refundGrossPaisa += lineGross;
                resolved.Add((line, soldItem, batch));
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

            List<GeneralLedgerEntry> originalLedger = await _context.GeneralLedgerEntries
                .Where(e => e.ReferenceNo == invoice.ReceiptNumber && e.TransactionType == "SALE")
                .ToListAsync(ct);

            if (originalLedger.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No SALE ledger entries found for invoice '{request.OriginalInvoiceNo}'.");
            }

            Guid originalGroupId = originalLedger[0].TransactionGroupId;
            string paymentAccount = LedgerAccounts.PaymentMethodToAccount(invoice.PaymentMethod);

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
            _context.SalesReturns.Add(salesReturn);

            foreach ((ReturnLineRequest line, SalesItem soldItem, ProductBatch batch) in resolved)
            {
                _context.SalesReturnItems.Add(new SalesReturnItem
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

                batch.CurrentQty += line.Quantity;
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
