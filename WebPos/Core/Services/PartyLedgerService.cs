using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public class PartyLedgerService : IPartyLedgerService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly ITenantService _tenantService;

    public PartyLedgerService(
        WebPosDbContext context,
        ITransactionService transactionService,
        ITenantService tenantService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task RecordTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(
            ct => RecordPartyTransactionAsync(
                partyId,
                amountPaisa,
                transactionType,
                paymentMethod,
                referenceNo,
                ct),
            cancellationToken);

    public async Task RecordPartyTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (amountPaisa <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero Paisa.");
        }

        Party? party = await _context.Parties.FirstOrDefaultAsync(
            p => p.Id == partyId && p.TenantId == _tenantService.TenantId,
            cancellationToken);
        if (party is null)
        {
            throw new KeyNotFoundException("Party profile not found in system storage.");
        }

        long oldBalance = party.CurrentBalancePaisa;
        long newBalance = oldBalance;

        string typeUpper = transactionType.ToUpperInvariant();
        string methodUpper = paymentMethod.ToUpperInvariant();

        if (methodUpper == "CREDIT")
        {
            string partyTypeUpper = party.PartyType?.ToUpperInvariant() ?? PartyTypes.Customer;

            if (partyTypeUpper == PartyTypes.Customer)
            {
                if (typeUpper == "SALE")
                {
                    newBalance = oldBalance + amountPaisa;
                }
                else if (typeUpper is "CUSTOMER_RETURN" or "PAYMENT")
                {
                    newBalance = oldBalance - amountPaisa;
                }
            }
            else if (partyTypeUpper == PartyTypes.Supplier)
            {
                if (typeUpper == "PURCHASE")
                {
                    newBalance = oldBalance + amountPaisa;
                }
                else if (typeUpper is "SUPPLIER_RETURN" or "PAYMENT")
                {
                    newBalance = oldBalance - amountPaisa;
                }
            }

            party.CurrentBalancePaisa = newBalance;
        }

        string? invoiceNo = typeUpper is "SALE" or "CUSTOMER_RETURN" ? referenceNo : null;

        Guid? purchaseOrderId = null;
        if (typeUpper is "PURCHASE" or "SUPPLIER_RETURN"
            && Guid.TryParse(referenceNo, out Guid parsedGuid))
        {
            purchaseOrderId = parsedGuid;
        }

        PartyLedger ledgerEntry = new()
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantService.TenantId,
            PartyId = partyId,
            InvoiceNo = invoiceNo,
            PurchaseOrderId = purchaseOrderId,
            Type = typeUpper,
            PaymentMethod = methodUpper,
            OldBalancePaisa = oldBalance,
            TransactionAmountPaisa = amountPaisa,
            NewBalancePaisa = newBalance,
            ReferenceDetails = referenceNo ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PartyLedgers.AddAsync(ledgerEntry, cancellationToken);
    }

    public async Task InitializeSupplierLedgerAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        Party party = await _context.Parties.FirstOrDefaultAsync(
            candidate =>
                candidate.Id == partyId
                && candidate.TenantId == _tenantService.TenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                "Supplier party was not found for ledger initialization.");

        if (!string.Equals(
                party.PartyType,
                PartyTypes.Supplier,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ledger auto-initialization is only allowed for SUPPLIER parties.");
        }

        bool alreadyInitialized = await _context.PartyLedgers.AnyAsync(
            entry =>
                entry.PartyId == partyId
                && entry.TenantId == _tenantService.TenantId
                && entry.Type == "OPENING",
            cancellationToken);

        if (alreadyInitialized)
        {
            return;
        }

        party.CurrentBalancePaisa = 0;

        await _context.PartyLedgers.AddAsync(
            new PartyLedger
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.TenantId,
                PartyId = partyId,
                InvoiceNo = null,
                PurchaseOrderId = null,
                Type = "OPENING",
                PaymentMethod = "CREDIT",
                OldBalancePaisa = 0,
                TransactionAmountPaisa = 0,
                NewBalancePaisa = 0,
                ReferenceDetails = "SUPPLIER_INIT",
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for party ledger operations.");
        }
    }
}
