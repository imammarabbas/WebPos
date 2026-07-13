using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public class PartyLedgerService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;

    public PartyLedgerService(WebPosDbContext context, ITransactionService transactionService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    }

    public Task RecordTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default) =>
        _transactionService.ExecuteInTransactionAsync(
            ct => RecordPartyTransactionAsync(partyId, amountPaisa, transactionType, paymentMethod, referenceNo, ct),
            cancellationToken);

    public async Task RecordPartyTransactionAsync(
        Guid partyId,
        long amountPaisa,
        string transactionType,
        string paymentMethod,
        string referenceNo,
        CancellationToken cancellationToken = default)
    {
        if (amountPaisa <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero Paisa.");
        }

        Party? party = await _context.Parties.FirstOrDefaultAsync(p => p.Id == partyId, cancellationToken);
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
            string partyTypeUpper = party.PartyType?.ToUpperInvariant() ?? "CUSTOMER";

            if (partyTypeUpper == "CUSTOMER")
            {
                if (typeUpper == "SALE")
                {
                    newBalance = oldBalance + amountPaisa;
                }
                else if (typeUpper == "CUSTOMER_RETURN" || typeUpper == "PAYMENT")
                {
                    newBalance = oldBalance - amountPaisa;
                }
            }
            else if (partyTypeUpper == "SUPPLIER")
            {
                if (typeUpper == "PURCHASE")
                {
                    newBalance = oldBalance + amountPaisa;
                }
                else if (typeUpper == "SUPPLIER_RETURN" || typeUpper == "PAYMENT")
                {
                    newBalance = oldBalance - amountPaisa;
                }
            }

            party.CurrentBalancePaisa = newBalance;
        }

        string? invoiceNo = typeUpper is "SALE" or "CUSTOMER_RETURN" ? referenceNo : null;

        Guid? purchaseOrderId = null;
        if ((typeUpper is "PURCHASE" or "SUPPLIER_RETURN") && Guid.TryParse(referenceNo, out Guid parsedGuid))
        {
            purchaseOrderId = parsedGuid;
        }

        PartyLedger ledgerEntry = new()
        {
            Id = Guid.NewGuid(),
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
}
