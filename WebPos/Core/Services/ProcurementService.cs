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

    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly IPartyLedgerService _partyLedgerService;
    private readonly IPartyService _partyService;
    private readonly ITenantService _tenantService;

    public ProcurementService(
        WebPosDbContext context,
        ITransactionService transactionService,
        IPartyLedgerService partyLedgerService,
        IPartyService partyService,
        ITenantService tenantService)
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
                request.CollectionTime ?? DateTimeOffset.UtcNow;

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
            _context.DailyMilkCollections.Add(collection);

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

            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            string paymentAccount = LedgerAccounts.PaymentMethodToAccount(paymentMethod);

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "SUPPLIER_PAYMENT",
                    ReferenceNo = request.ReferenceNo.Trim(),
                    ReferenceDetails = $"Supplier payment {request.ReferenceNo.Trim()}",
                    ShiftId = request.ShiftId,
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

            await _partyLedgerService.RecordPartyTransactionAsync(
                request.SupplierId,
                request.AmountPaisa,
                "PAYMENT",
                paymentMethod,
                request.ReferenceNo.Trim(),
                ct);

            if (request.ShiftId is Guid shiftId
                && LedgerAccounts.IsCashAccount(paymentAccount))
            {
                CashierShift shift = await _context.CashierShifts
                    .FirstOrDefaultAsync(
                        candidate =>
                            candidate.Id == shiftId
                            && candidate.TenantId == _tenantService.TenantId
                            && candidate.Status == "OPEN",
                        ct)
                    ?? throw new InvalidOperationException(
                        "Open shift was not found for the cash supplier payment.");

                shift.ExpectedCashPaisa -= request.AmountPaisa;
            }

            return new RecordSupplierPaymentResult
            {
                SupplierId = request.SupplierId,
                AmountPaisa = request.AmountPaisa,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);
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
