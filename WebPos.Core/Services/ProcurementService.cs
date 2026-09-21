using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class ProcurementService : IProcurementService
{
    private static readonly HashSet<string> AllowedMilkTypes =
        new(StringComparer.OrdinalIgnoreCase) { "COW", "BUFFALO", "MIXED" };

    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly IPartyService _partyService;
    private readonly ICashAccountService _cashAccountService;
    private readonly ITenantService _tenantService;

    public ProcurementService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        IPartyService partyService,
        ICashAccountService cashAccountService,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _partyLedgerService = partyLedgerService
            ?? throw new ArgumentNullException(nameof(partyLedgerService));
        _partyService = partyService
            ?? throw new ArgumentNullException(nameof(partyService));
        _cashAccountService = cashAccountService
            ?? throw new ArgumentNullException(nameof(cashAccountService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<RecordMilkCollectionResult> RecordMilkCollectionAsync(
        RecordMilkCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidateMilkCollection(request);

            PartyDto supplier = await _partyService.GetSupplierAsync(
                request.SupplierId,
                ct);
            if (!string.Equals(
                    supplier.Role,
                    PartyTypes.Supplier,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Milk collection requires a SUPPLIER party.");
            }

            long totalCreditPaisa = (long)Math.Round(
                request.LitersReceived * request.RatePerLiterPaisa,
                MidpointRounding.AwayFromZero);

            if (totalCreditPaisa <= 0)
            {
                throw new InvalidOperationException(
                    "Milk collection credit must be greater than zero.");
            }

            Guid collectionId = Guid.NewGuid();
            // Non-GUID reference so PartyLedger does not treat this as a PurchaseOrderId.
            string referenceNo = $"MILK-{collectionId:N}";
            DateTimeOffset collectionTime =
                (request.CollectionTime ?? DateTimeOffset.UtcNow).ToUniversalTime();

            DailyMilkCollection collection = new()
            {
                Id = collectionId,
                TenantId = _tenantService.TenantId,
                SupplierId = request.SupplierId,
                MilkType = request.MilkType.Trim().ToUpperInvariant(),
                LitersReceived = request.LitersReceived,
                FatPercent = request.FatPercent,
                SnfPercent = request.SnfPercent,
                RatePerLiterPaisa = request.RatePerLiterPaisa,
                TotalCreditPaisa = totalCreditPaisa,
                CollectionTime = collectionTime
            };
            _ambient.Required.DailyMilkCollections.Add(collection);

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "MILK_COLLECTION",
                    ReferenceNo = referenceNo,
                    ReferenceDetails =
                        $"Milk collection {collection.MilkType} {request.LitersReceived}L",
                    PartyId = request.SupplierId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.Inventory,
                            DebitPaisa = totalCreditPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.AccountsPayable,
                            DebitPaisa = 0,
                            CreditPaisa = totalCreditPaisa
                        }
                    ]
                },
                ct);

            await _partyLedgerService.RecordPartyTransactionAsync(
                request.SupplierId,
                totalCreditPaisa,
                "PURCHASE",
                "CREDIT",
                referenceNo,
                ct);

            return new RecordMilkCollectionResult
            {
                CollectionId = collectionId,
                TotalCreditPaisa = totalCreditPaisa,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);
    }

    public Task<RecordSupplierPaymentResult> RecordSupplierPaymentAsync(
        RecordSupplierPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            if (request.SupplierId == Guid.Empty)
            {
                throw new ArgumentException("Supplier is required.", nameof(request));
            }

            if (request.AmountPaisa <= 0)
            {
                throw new ArgumentException(
                    "Payment amount must be greater than zero Paisa.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                throw new ArgumentException(
                    "Payment method is required.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ReferenceNo))
            {
                throw new ArgumentException(
                    "Reference number is required.",
                    nameof(request));
            }

            if (string.Equals(
                    request.PaymentMethod,
                    "CREDIT",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Supplier payments cannot use CREDIT as the payment channel.");
            }

            PartyDto supplier = await _partyService.GetSupplierAsync(
                request.SupplierId,
                ct);
            if (!string.Equals(
                    supplier.Role,
                    PartyTypes.Supplier,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Supplier payments require a SUPPLIER party.");
            }

            IReadOnlyList<PaymentAllocationRequest> allocations =
                NormalizeAllocations(request.Allocations, request.AmountPaisa, forCustomer: false);

            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            CashPaymentResolution payment = await _cashAccountService.ResolvePaymentAccountAsync(
                request.CashAccountId,
                request.AccountCode,
                paymentMethod,
                ct);
            string paymentAccount = payment.AccountCode;

            CashierShift? tillShift = null;
            if (payment.AffectsTillDrawer)
            {
                if (request.ShiftId is Guid payShiftId)
                {
                    tillShift = await CashierShiftLocking.LockByIdForUpdateAsync(
                        _ambient.Required,
                        payShiftId,
                        _tenantService.TenantId,
                        ct)
                        ?? throw new InvalidOperationException(
                            "Open shift was not found for the cash supplier payment.");
                }
                else if (payment.TerminalId is Guid tillTerminal)
                {
                    tillShift = await CashierShiftLocking.LockOpenByTerminalForUpdateAsync(
                        _ambient.Required,
                        tillTerminal,
                        _tenantService.TenantId,
                        ct);
                }

                if (tillShift is null
                    || !string.Equals(tillShift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Open shift was not found for the cash supplier payment.");
                }

                if (payment.TerminalId is Guid expectedTerminal
                    && tillShift.TerminalId != expectedTerminal)
                {
                    throw new InvalidOperationException(
                        "Shift terminal does not match the selected till cash account.");
                }

                long gl = await SumAmbientGlAsync(paymentAccount, ct);
                long spendable = CashSpendable.ForTill(gl, tillShift.ExpectedCashPaisa);
                CashSpendable.EnsureCanSpend(
                    paymentAccount,
                    spendable,
                    request.AmountPaisa,
                    tillShift.Id,
                    gl,
                    tillShift.ExpectedCashPaisa);

                await TillPhysicalCash.RecognizeIntoGlIfNeededAsync(
                    _transactionService,
                    gl,
                    paymentAccount,
                    request.AmountPaisa,
                    tillShift.Id,
                    request.ReferenceNo.Trim(),
                    ct);
            }
            else
            {
                await EnsureNonTillFundingSufficientAsync(paymentAccount, request.AmountPaisa, ct);
            }

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "SUPPLIER_PAYMENT",
                    ReferenceNo = request.ReferenceNo.Trim(),
                    ReferenceDetails = $"Supplier payment {request.ReferenceNo.Trim()}",
                    ShiftId = tillShift?.Id ?? request.ShiftId,
                    PartyId = request.SupplierId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.AccountsPayable,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = paymentAccount,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                },
                ct);

            Guid partyLedgerId = await _partyLedgerService.RecordPartyTransactionAsync(
                request.SupplierId,
                request.AmountPaisa,
                "PAYMENT",
                paymentMethod,
                request.ReferenceNo.Trim(),
                ct);

            await ApplySupplierAllocationsAsync(
                request.SupplierId,
                partyLedgerId,
                allocations,
                ct);

            if (tillShift is not null)
            {
                tillShift.ExpectedCashPaisa -= request.AmountPaisa;
            }

            return new RecordSupplierPaymentResult
            {
                SupplierId = request.SupplierId,
                AmountPaisa = request.AmountPaisa,
                TransactionGroupId = transactionGroupId,
                PartyLedgerId = partyLedgerId
            };
        }, cancellationToken);
    }

    public Task<RecordCustomerPaymentResult> RecordCustomerPaymentAsync(
        RecordCustomerPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            if (request.CustomerId == Guid.Empty)
            {
                throw new ArgumentException("Customer is required.", nameof(request));
            }

            if (request.AmountPaisa <= 0)
            {
                throw new ArgumentException(
                    "Payment amount must be greater than zero Paisa.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            {
                throw new ArgumentException(
                    "Payment method is required.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ReferenceNo))
            {
                throw new ArgumentException(
                    "Reference number is required.",
                    nameof(request));
            }

            if (string.Equals(
                    request.PaymentMethod,
                    "CREDIT",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Customer payments cannot use CREDIT as the payment channel.");
            }

            PartyDto customer = await _partyService.GetPartyAsync(request.CustomerId, ct);
            if (!string.Equals(
                    customer.Role,
                    PartyTypes.Customer,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Customer payments require a CUSTOMER party.");
            }

            IReadOnlyList<PaymentAllocationRequest> allocations =
                NormalizeAllocations(request.Allocations, request.AmountPaisa, forCustomer: true);

            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            CashPaymentResolution payment = await _cashAccountService.ResolvePaymentAccountAsync(
                request.CashAccountId,
                request.AccountCode,
                paymentMethod,
                ct);
            string paymentAccount = payment.AccountCode;

            if (payment.AffectsTillDrawer && request.ShiftId is null)
            {
                throw new InvalidOperationException(
                    "Select an open shift for till-funded customer payments.");
            }

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "CUSTOMER_PAYMENT",
                    ReferenceNo = request.ReferenceNo.Trim(),
                    ReferenceDetails = $"Customer payment {request.ReferenceNo.Trim()}",
                    ShiftId = request.ShiftId,
                    PartyId = request.CustomerId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = paymentAccount,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.AccountsReceivable,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                },
                ct);

            Guid partyLedgerId = await _partyLedgerService.RecordPartyTransactionAsync(
                request.CustomerId,
                request.AmountPaisa,
                "PAYMENT",
                paymentMethod,
                request.ReferenceNo.Trim(),
                ct);

            await ApplyCustomerAllocationsAsync(
                request.CustomerId,
                partyLedgerId,
                allocations,
                ct);

            if (request.ShiftId is Guid shiftId && payment.AffectsTillDrawer)
            {
                CashierShift shift = await _ambient.Required.CashierShifts
                    .FirstOrDefaultAsync(
                        candidate =>
                            candidate.Id == shiftId
                            && candidate.TenantId == _tenantService.TenantId
                            && candidate.Status == "OPEN",
                        ct)
                    ?? throw new InvalidOperationException(
                        "Open shift was not found for the cash customer payment.");

                if (payment.TerminalId is Guid tillTerminal
                    && shift.TerminalId != tillTerminal)
                {
                    throw new InvalidOperationException(
                        "Shift terminal does not match the selected till cash account.");
                }

                shift.ExpectedCashPaisa += request.AmountPaisa;
            }

            return new RecordCustomerPaymentResult
            {
                CustomerId = request.CustomerId,
                AmountPaisa = request.AmountPaisa,
                TransactionGroupId = transactionGroupId,
                PartyLedgerId = partyLedgerId
            };
        }, cancellationToken);
    }

    public Task<RecordPartyAdjustmentResult> RecordPartyAdjustmentAsync(
        RecordPartyAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            if (request.PartyId == Guid.Empty)
            {
                throw new ArgumentException("Party is required.", nameof(request));
            }

            if (request.AmountPaisa <= 0)
            {
                throw new ArgumentException(
                    "Adjustment amount must be greater than zero Paisa.",
                    nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Notes))
            {
                throw new ArgumentException("Notes are required.", nameof(request));
            }

            PartyDto party = await _partyService.GetPartyAsync(request.PartyId, ct);
            bool isCustomer = string.Equals(
                party.Role,
                PartyTypes.Customer,
                StringComparison.OrdinalIgnoreCase);
            bool isSupplier = string.Equals(
                party.Role,
                PartyTypes.Supplier,
                StringComparison.OrdinalIgnoreCase);
            if (!isCustomer && !isSupplier)
            {
                throw new InvalidOperationException("Adjustments require a CUSTOMER or SUPPLIER party.");
            }

            string notes = request.Notes.Trim();
            string referenceNo = notes.StartsWith("ADJ-", StringComparison.OrdinalIgnoreCase)
                ? notes
                : $"ADJ-{notes}";
            string clearingAccount = LedgerAccounts.Expense(ExpenseCategories.Other);
            string partyAccount = isCustomer
                ? LedgerAccounts.AccountsReceivable
                : LedgerAccounts.AccountsPayable;

            // Increase due: Dr AR/AP, Cr clearing. Reduce due: Dr clearing, Cr AR/AP (customer)
            // Supplier increase: Dr clearing, Cr AP. Supplier reduce: Dr AP, Cr clearing.
            List<LedgerPosting> postings;
            if (isCustomer)
            {
                postings = request.IncreaseBalance
                    ?
                    [
                        new LedgerPosting
                        {
                            AccountCode = partyAccount,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = clearingAccount,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                    :
                    [
                        new LedgerPosting
                        {
                            AccountCode = clearingAccount,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = partyAccount,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ];
            }
            else
            {
                postings = request.IncreaseBalance
                    ?
                    [
                        new LedgerPosting
                        {
                            AccountCode = clearingAccount,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = partyAccount,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                    :
                    [
                        new LedgerPosting
                        {
                            AccountCode = partyAccount,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = clearingAccount,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ];
            }

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "PARTY_ADJUSTMENT",
                    ReferenceNo = referenceNo,
                    ReferenceDetails = notes,
                    PartyId = request.PartyId,
                    Postings = postings
                },
                ct);

            string ledgerMethod = request.IncreaseBalance ? "DEBIT" : "CREDIT";
            Guid partyLedgerId = await _partyLedgerService.RecordPartyTransactionAsync(
                request.PartyId,
                request.AmountPaisa,
                "ADJUSTMENT",
                ledgerMethod,
                referenceNo,
                ct);

            return new RecordPartyAdjustmentResult
            {
                PartyId = request.PartyId,
                AmountPaisa = request.AmountPaisa,
                TransactionGroupId = transactionGroupId,
                PartyLedgerId = partyLedgerId
            };
        }, cancellationToken);
    }

    private async Task ApplyCustomerAllocationsAsync(
        Guid customerId,
        Guid partyLedgerId,
        IReadOnlyList<PaymentAllocationRequest> allocations,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid tenantId = _tenantService.TenantId;

        foreach (PaymentAllocationRequest allocation in allocations)
        {
            string invoiceNo = allocation.InvoiceNo!.Trim();
            SalesInvoice invoice = await _ambient.Required.SalesInvoices
                .FirstOrDefaultAsync(
                    i =>
                        i.InvoiceNo == invoiceNo
                        && i.TenantId == tenantId
                        && i.CustomerId == customerId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Credit invoice '{invoiceNo}' was not found for this customer.");

            if (!string.Equals(invoice.PaymentMethod, "CREDIT", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Invoice '{invoiceNo}' is not a credit invoice.");
            }

            long outstanding = invoice.TotalAmountPaisa - invoice.AmountPaidPaisa;
            if (allocation.AmountPaisa > outstanding)
            {
                throw new InvalidOperationException(
                    $"Allocation {allocation.AmountPaisa} exceeds outstanding {outstanding} on '{invoiceNo}'.");
            }

            invoice.AmountPaidPaisa += allocation.AmountPaisa;
            await _ambient.Required.PartyPaymentAllocations.AddAsync(
                new PartyPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartyLedgerId = partyLedgerId,
                    InvoiceNo = invoiceNo,
                    PurchaseOrderId = null,
                    AmountPaisa = allocation.AmountPaisa,
                    CreatedAt = now
                },
                cancellationToken);
        }
    }

    private async Task ApplySupplierAllocationsAsync(
        Guid supplierId,
        Guid partyLedgerId,
        IReadOnlyList<PaymentAllocationRequest> allocations,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid tenantId = _tenantService.TenantId;

        foreach (PaymentAllocationRequest allocation in allocations)
        {
            Guid purchaseOrderId = allocation.PurchaseOrderId!.Value;
            PurchaseOrder order = await _ambient.Required.PurchaseOrders
                .FirstOrDefaultAsync(
                    o =>
                        o.Id == purchaseOrderId
                        && o.TenantId == tenantId
                        && o.SupplierId == supplierId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Purchase order '{purchaseOrderId}' was not found for this supplier.");

            if (!order.IsReceived)
            {
                throw new InvalidOperationException(
                    $"Purchase order '{order.SupplierInvoiceNo}' has not been received yet.");
            }

            long outstanding = order.NetPayablePaisa - order.AmountPaidPaisa;
            if (allocation.AmountPaisa > outstanding)
            {
                throw new InvalidOperationException(
                    $"Allocation {allocation.AmountPaisa} exceeds outstanding {outstanding} on '{order.SupplierInvoiceNo}'.");
            }

            order.AmountPaidPaisa += allocation.AmountPaisa;
            order.PaymentStatus = order.AmountPaidPaisa >= order.NetPayablePaisa
                ? "PAID"
                : "PARTIAL";

            await _ambient.Required.PartyPaymentAllocations.AddAsync(
                new PartyPaymentAllocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartyLedgerId = partyLedgerId,
                    InvoiceNo = null,
                    PurchaseOrderId = purchaseOrderId,
                    AmountPaisa = allocation.AmountPaisa,
                    CreatedAt = now
                },
                cancellationToken);
        }
    }

    private static IReadOnlyList<PaymentAllocationRequest> NormalizeAllocations(
        IReadOnlyList<PaymentAllocationRequest>? allocations,
        long paymentAmountPaisa,
        bool forCustomer)
    {
        if (allocations is null || allocations.Count == 0)
        {
            return [];
        }

        List<PaymentAllocationRequest> normalized = [];
        long sum = 0;
        foreach (PaymentAllocationRequest allocation in allocations)
        {
            if (allocation.AmountPaisa <= 0)
            {
                throw new ArgumentException(
                    "Each allocation amount must be greater than zero Paisa.");
            }

            bool hasInvoice = !string.IsNullOrWhiteSpace(allocation.InvoiceNo);
            bool hasPo = allocation.PurchaseOrderId is Guid id && id != Guid.Empty;
            if (hasInvoice == hasPo)
            {
                throw new ArgumentException(
                    "Each allocation must reference exactly one invoice or purchase order.");
            }

            if (forCustomer && !hasInvoice)
            {
                throw new ArgumentException(
                    "Customer payment allocations require InvoiceNo.");
            }

            if (!forCustomer && !hasPo)
            {
                throw new ArgumentException(
                    "Supplier payment allocations require PurchaseOrderId.");
            }

            sum += allocation.AmountPaisa;
            normalized.Add(allocation);
        }

        if (sum != paymentAmountPaisa)
        {
            throw new ArgumentException(
                $"Allocation total ({sum}) must equal payment amount ({paymentAmountPaisa}).");
        }

        return normalized;
    }

    private async Task EnsureNonTillFundingSufficientAsync(
        string accountCode,
        long amountPaisa,
        CancellationToken cancellationToken)
    {
        long gl = await SumAmbientGlAsync(accountCode, cancellationToken);
        CashSpendable.EnsureCanSpend(
            CashAccountService.SanitizeAccountCode(accountCode),
            CashSpendable.ForNonTill(gl),
            amountPaisa);
    }

    private async Task<long> SumAmbientGlAsync(
        string accountCode,
        CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.TenantId;
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = _ambient.Required.GeneralLedgerEntries
            .Where(e => e.TenantId == tenantId && e.AccountCode.ToUpper() == code);

        long? debits = await query.SumAsync(e => (long?)e.DebitPaisa, cancellationToken);
        long? credits = await query.SumAsync(e => (long?)e.CreditPaisa, cancellationToken);
        return (debits ?? 0L) - (credits ?? 0L);
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for procurement.");
        }
    }

    private static void ValidateMilkCollection(RecordMilkCollectionRequest request)
    {
        if (request.SupplierId == Guid.Empty)
        {
            throw new ArgumentException("Supplier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.MilkType)
            || !AllowedMilkTypes.Contains(request.MilkType.Trim()))
        {
            throw new ArgumentException(
                "Milk type must be COW, BUFFALO, or MIXED.",
                nameof(request));
        }

        if (request.LitersReceived <= 0)
        {
            throw new ArgumentException(
                "Liters received must be greater than zero.",
                nameof(request));
        }

        if (request.RatePerLiterPaisa <= 0)
        {
            throw new ArgumentException(
                "Rate per liter must be greater than zero Paisa.",
                nameof(request));
        }

        if (request.FatPercent is < 0 or > 100)
        {
            throw new ArgumentException(
                "Fat percent must be between 0 and 100.",
                nameof(request));
        }

        if (request.SnfPercent is < 0 or > 100)
        {
            throw new ArgumentException(
                "SNF percent must be between 0 and 100.",
                nameof(request));
        }
    }
}
