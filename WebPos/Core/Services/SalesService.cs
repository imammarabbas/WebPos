using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class SalesService : ISalesService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly PartyLedgerService _partyLedgerService;

    public SalesService(
        WebPosDbContext context,
        ITransactionService transactionService,
        PartyLedgerService partyLedgerService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService ?? throw new ArgumentNullException(nameof(partyLedgerService));
    }

    public Task<CompleteSaleResult> CompleteSaleAsync(CompleteSaleRequest request, CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidateRequest(request);

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

            CashierShift? shift = await _context.CashierShifts
                .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

            if (shift is null || !string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("An open cashier shift is required to complete a sale.");
            }

            if (string.Equals(request.PaymentMethod, "CREDIT", StringComparison.OrdinalIgnoreCase))
            {
                if (request.CustomerId is null)
                {
                    throw new InvalidOperationException("CREDIT sales require a customer.");
                }

                Party? customer = await _context.Parties
                    .FirstOrDefaultAsync(p => p.Id == request.CustomerId.Value, ct);

                if (customer is null || !string.Equals(customer.PartyType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CREDIT sales require a valid customer party profile.");
                }
            }

            List<Guid> batchIds = request.Lines.Select(l => l.BatchId).Distinct().ToList();
            Dictionary<Guid, ProductBatch> batches = await _context.ProductBatches
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, ct);

            foreach (SaleLineRequest line in request.Lines)
            {
                if (!batches.TryGetValue(line.BatchId, out ProductBatch? batch))
                {
                    throw new InvalidOperationException($"Batch {line.BatchNumber} was not found.");
                }

                if (line.Quantity > batch.CurrentQty)
                {
                    throw new InvalidOperationException(
                        $"Insufficient stock for {line.ProductName} (batch {line.BatchNumber}). Available: {batch.CurrentQty}, requested: {line.Quantity}.");
                }
            }

            string receiptNumber = request.InvoiceNo;
            DateTimeOffset now = DateTimeOffset.UtcNow;

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
                PaymentMethod = request.PaymentMethod.ToUpperInvariant(),
                CreatedAt = now
            };
            _context.SalesInvoices.Add(invoice);

            foreach (SaleLineRequest line in request.Lines)
            {
                ProductBatch batch = batches[line.BatchId];

                SalesItem salesItem = new()
                {
                    Id = Guid.NewGuid(),
                    InvoiceNo = request.InvoiceNo,
                    ProductId = line.ProductId,
                    BatchId = line.BatchId,
                    Quantity = line.Quantity,
                    UnitPricePaisa = line.UnitPricePaisa,
                    DiscountAppliedPaisa = line.DiscountAppliedPaisa
                };
                _context.SalesItems.Add(salesItem);
                batch.CurrentQty -= line.Quantity;
            }

            string paymentAccount = LedgerAccounts.PaymentMethodToAccount(request.PaymentMethod);
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

            if (LedgerAccounts.IsCashAccount(paymentAccount))
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
