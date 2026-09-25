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

            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            bool isCredit = string.Equals(paymentMethod, "CREDIT", StringComparison.OrdinalIgnoreCase);
            bool isCash = string.Equals(paymentMethod, "CASH", StringComparison.OrdinalIgnoreCase);

            long amountPaid;
            long changePaisa = 0L;
            long customerCreditApplied = 0L;
            long unpaidOnAccountPaisa = 0L;
            bool registeredUnderpay = false;

            if (isCredit)
            {
                if (request.CustomerId is null)
                {
                    throw new InvalidOperationException("CREDIT sales require a customer.");
                }

                amountPaid = 0L;
            }
            else
            {
                amountPaid = request.AmountPaidPaisa ?? netPaisa;

                long previousBalancePaisa = 0L;
                if (request.CustomerId is Guid balanceCustomerId)
                {
                    Party? balanceCustomer = await context.Parties
                        .FirstOrDefaultAsync(p => p.Id == balanceCustomerId, ct);
                    previousBalancePaisa = balanceCustomer?.CurrentBalancePaisa ?? 0L;
                }

                if (amountPaid < netPaisa)
                {
                    if (request.CustomerId is null)
                    {
                        throw new InvalidOperationException(
                            "Amount paid must cover the sale total.");
                    }

                    // Registered underpay: remainder stays on customer ledger.
                    registeredUnderpay = true;
                    unpaidOnAccountPaisa = netPaisa - amountPaid;
                    changePaisa = 0L;
                    customerCreditApplied = 0L;
                }
                else
                {
                    long excessOverNet = amountPaid - netPaisa;
                    if (excessOverNet > 0)
                    {
                        if (request.ApplyExcessAsCustomerCredit)
                        {
                            if (request.CustomerId is null)
                            {
                                throw new InvalidOperationException(
                                    "A registered customer is required to credit excess payment.");
                            }

                            changePaisa = 0L;
                            customerCreditApplied = excessOverNet;
                        }
                        else
                        {
                            changePaisa = Math.Max(0L, excessOverNet - Math.Max(0L, previousBalancePaisa));
                            customerCreditApplied = excessOverNet - changePaisa;
                            if (customerCreditApplied > 0 && request.CustomerId is null)
                            {
                                customerCreditApplied = 0L;
                                changePaisa = excessOverNet;
                            }
                        }
                    }
                }
            }

            if (request.CustomerId is Guid requiredCustomerId
                && (isCredit || customerCreditApplied > 0 || registeredUnderpay))
            {
                Party? customer = await context.Parties
                    .FirstOrDefaultAsync(p => p.Id == requiredCustomerId, ct);

                if (customer is null
                    || !string.Equals(customer.PartyType, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        isCredit || registeredUnderpay
                            ? "CREDIT sales require a valid customer party profile."
                            : "Customer payment against account requires a valid customer party profile.");
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
                AmountPaidPaisa = isCredit ? 0 : (registeredUnderpay ? amountPaid : netPaisa),
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
            if (isCash)
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
                    isCredit ? null : request.CashAccountId,
                    paymentMethod,
                    ct);
                paymentAccount = payment.AccountCode;
                affectsTill = payment.AffectsTillDrawer;
            }

            string saleDetails = $"Sale invoice {request.InvoiceNo}";
            if (!string.IsNullOrWhiteSpace(request.OnlineTxnRef))
            {
                saleDetails = $"{saleDetails}; txn {request.OnlineTxnRef.Trim()}";
            }

            List<LedgerPosting> postings = [];
            if (registeredUnderpay)
            {
                if (amountPaid > 0)
                {
                    postings.Add(new LedgerPosting
                    {
                        AccountCode = paymentAccount,
                        DebitPaisa = amountPaid,
                        CreditPaisa = 0
                    });
                }

                if (unpaidOnAccountPaisa > 0)
                {
                    postings.Add(new LedgerPosting
                    {
                        AccountCode = LedgerAccounts.AccountsReceivable,
                        DebitPaisa = unpaidOnAccountPaisa,
                        CreditPaisa = 0
                    });
                }

                postings.Add(new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Revenue,
                    DebitPaisa = 0,
                    CreditPaisa = grossPaisa
                });
            }
            else
            {
                postings.Add(new LedgerPosting
                {
                    AccountCode = paymentAccount,
                    DebitPaisa = netPaisa,
                    CreditPaisa = 0
                });
                postings.Add(new LedgerPosting
                {
                    AccountCode = LedgerAccounts.Revenue,
                    DebitPaisa = 0,
                    CreditPaisa = grossPaisa
                });
            }

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
                    ReferenceDetails = saleDetails,
                    ShiftId = request.ShiftId,
                    PartyId = request.CustomerId,
                    Postings = postings
                },
                ct);

            if (request.CustomerId is Guid partyId)
            {
                if (registeredUnderpay)
                {
                    // Full sale on AR, then cash/online payment reduces balance.
                    await _partyLedgerService.RecordPartyTransactionAsync(
                        partyId,
                        netPaisa,
                        "SALE",
                        "CREDIT",
                        request.InvoiceNo,
                        ct);

                    if (amountPaid > 0)
                    {
                        await _partyLedgerService.RecordPartyTransactionAsync(
                            partyId,
                            amountPaid,
                            "PAYMENT",
                            paymentMethod,
                            $"{receiptNumber}-PAY",
                            ct);
                    }
                }
                else
                {
                    await _partyLedgerService.RecordPartyTransactionAsync(
                        partyId,
                        netPaisa,
                        "SALE",
                        paymentMethod,
                        request.InvoiceNo,
                        ct);
                }
            }

            if (affectsTill)
            {
                shift.ExpectedCashPaisa += registeredUnderpay ? amountPaid : netPaisa;
            }

            if (customerCreditApplied > 0 && request.CustomerId is Guid creditPartyId)
            {
                string excessRef = $"{receiptNumber}-ADV";
                await _transactionService.PostBalancedEntriesAsync(
                    new DoubleEntryPostRequest
                    {
                        TransactionType = "CUSTOMER_PAYMENT",
                        ReferenceNo = excessRef,
                        ReferenceDetails = $"Excess from sale {request.InvoiceNo}",
                        ShiftId = request.ShiftId,
                        PartyId = creditPartyId,
                        Postings =
                        [
                            new LedgerPosting
                            {
                                AccountCode = paymentAccount,
                                DebitPaisa = customerCreditApplied,
                                CreditPaisa = 0
                            },
                            new LedgerPosting
                            {
                                AccountCode = LedgerAccounts.AccountsReceivable,
                                DebitPaisa = 0,
                                CreditPaisa = customerCreditApplied
                            }
                        ]
                    },
                    ct);

                await _partyLedgerService.RecordPartyTransactionAsync(
                    creditPartyId,
                    customerCreditApplied,
                    "PAYMENT",
                    paymentMethod,
                    excessRef,
                    ct);

                if (affectsTill)
                {
                    shift.ExpectedCashPaisa += customerCreditApplied;
                }
            }

            long? customerBalance = null;
            if (request.CustomerId is Guid balancePartyId)
            {
                Party? balanceParty = await context.Parties
                    .FirstOrDefaultAsync(p => p.Id == balancePartyId, ct);
                customerBalance = balanceParty?.CurrentBalancePaisa;
            }

            return new CompleteSaleResult
            {
                InvoiceNo = request.InvoiceNo,
                ReceiptNumber = receiptNumber,
                TotalAmountPaisa = netPaisa,
                GrossAmountPaisa = grossPaisa,
                DiscountAmountPaisa = request.DiscountAmountPaisa,
                TransactionGroupId = transactionGroupId,
                AmountPaidPaisa = amountPaid,
                ChangePaisa = changePaisa,
                CustomerCreditAppliedPaisa = customerCreditApplied,
                CustomerBalancePaisa = customerBalance
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
