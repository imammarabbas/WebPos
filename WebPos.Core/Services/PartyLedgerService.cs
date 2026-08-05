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
            async ct =>
            {
                _ = await RecordPartyTransactionAsync(
                    partyId,
                    amountPaisa,
                    transactionType,
                    paymentMethod,
                    referenceNo,
                    ct);
            },
            cancellationToken);

    public async Task<Guid> RecordPartyTransactionAsync(
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
        string partyTypeUpper = party.PartyType?.ToUpperInvariant() ?? PartyTypes.Customer;

        // Balance follows transaction type (not only CREDIT payment method),
        // so cash/bank settlements correctly reduce AR/AP.
        if (typeUpper == "ADJUSTMENT")
        {
            // DEBIT increases due; CREDIT (default) decreases due.
            newBalance = methodUpper == "DEBIT"
                ? oldBalance + amountPaisa
                : oldBalance - amountPaisa;
        }
        else if (partyTypeUpper == PartyTypes.Customer)
        {
            if (typeUpper == "SALE" && methodUpper == "CREDIT")
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

        string? invoiceNo = typeUpper is "SALE" or "CUSTOMER_RETURN" ? referenceNo : null;

        Guid? purchaseOrderId = null;
        if (typeUpper is "PURCHASE" or "SUPPLIER_RETURN"
            && Guid.TryParse(referenceNo, out Guid parsedGuid))
        {
            purchaseOrderId = parsedGuid;
        }

        (long debitPaisa, long creditPaisa) = ResolveDebitCredit(oldBalance, newBalance, amountPaisa);

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
            DebitPaisa = debitPaisa,
            CreditPaisa = creditPaisa,
            NewBalancePaisa = newBalance,
            ReferenceDetails = referenceNo ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PartyLedgers.AddAsync(ledgerEntry, cancellationToken);
        return ledgerEntry.Id;
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
                DebitPaisa = 0,
                CreditPaisa = 0,
                NewBalancePaisa = 0,
                ReferenceDetails = "SUPPLIER_INIT",
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<PartyLedgerEntryDto>> ListByPartyAsync(
        Guid partyId,
        int limit = 50,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("Party id is required.", nameof(partyId));
        }

        Party party = await LoadPartyAsync(partyId, cancellationToken);
        int take = Math.Clamp(limit, 1, 500);

        IQueryable<PartyLedger> query = _context.PartyLedgers
            .AsNoTracking()
            .Where(e => e.PartyId == partyId && e.TenantId == _tenantService.TenantId);

        if (from is DateTimeOffset fromValue)
        {
            DateTime fromUtc = fromValue.UtcDateTime;
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }

        if (to is DateTimeOffset toValue)
        {
            DateTime toUtc = toValue.UtcDateTime;
            query = query.Where(e => e.CreatedAt <= toUtc);
        }

        bool chronological = from is not null || to is not null;
        query = chronological
            ? query.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            : query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id);

        List<PartyLedger> entries = await query
            .Take(take)
            .ToListAsync(cancellationToken);

        return entries.Select(e => MapEntry(e, party.PartyType)).ToList();
    }

    public async Task<PartyLedgerSlipDto> GetSlipAsync(
        Guid partyId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("Party id is required.", nameof(partyId));
        }

        if (year < 2000 || year > 2100 || month is < 1 or > 12)
        {
            throw new ArgumentException("Year/month must be a valid calendar month.");
        }

        Party party = await LoadPartyAsync(partyId, cancellationToken);

        DateTime periodStart = new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime periodEnd = periodStart.AddMonths(1);

        long openingBalance = await _context.PartyLedgers
            .AsNoTracking()
            .Where(e =>
                e.PartyId == partyId
                && e.TenantId == _tenantService.TenantId
                && e.CreatedAt < periodStart)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Select(e => (long?)e.NewBalancePaisa)
            .FirstOrDefaultAsync(cancellationToken) ?? 0L;

        List<PartyLedger> entries = await _context.PartyLedgers
            .AsNoTracking()
            .Where(e =>
                e.PartyId == partyId
                && e.TenantId == _tenantService.TenantId
                && e.CreatedAt >= periodStart
                && e.CreatedAt < periodEnd)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        List<PartyLedgerEntryDto> mapped = entries
            .Select(e => MapEntry(e, party.PartyType))
            .ToList();

        long closingBalance = mapped.Count > 0
            ? mapped[^1].NewBalancePaisa
            : openingBalance;

        return new PartyLedgerSlipDto
        {
            PartyId = party.Id,
            PartyName = party.Name,
            PartyType = party.PartyType,
            Year = year,
            Month = month,
            OpeningBalancePaisa = openingBalance,
            ClosingBalancePaisa = closingBalance,
            TotalDebitPaisa = mapped.Sum(e => e.DebitPaisa),
            TotalCreditPaisa = mapped.Sum(e => e.CreditPaisa),
            Entries = mapped
        };
    }

    public async Task<IReadOnlyList<OpenPartySlipDto>> ListOpenSlipsAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Party party = await LoadPartyAsync(partyId, cancellationToken);
        Guid tenantId = _tenantService.TenantId;

        if (string.Equals(party.PartyType, PartyTypes.Customer, StringComparison.OrdinalIgnoreCase))
        {
            return await _context.SalesInvoices
                .AsNoTracking()
                .Where(i =>
                    i.TenantId == tenantId
                    && i.CustomerId == partyId
                    && i.PaymentMethod == "CREDIT"
                    && i.AmountPaidPaisa < i.TotalAmountPaisa)
                .OrderBy(i => i.CreatedAt)
                .Select(i => new OpenPartySlipDto
                {
                    InvoiceNo = i.InvoiceNo,
                    PurchaseOrderId = null,
                    Reference = i.InvoiceNo,
                    CreatedAt = i.CreatedAt,
                    TotalPaisa = i.TotalAmountPaisa,
                    AmountPaidPaisa = i.AmountPaidPaisa,
                    OutstandingPaisa = i.TotalAmountPaisa - i.AmountPaidPaisa
                })
                .ToListAsync(cancellationToken);
        }

        if (string.Equals(party.PartyType, PartyTypes.Supplier, StringComparison.OrdinalIgnoreCase))
        {
            return await _context.PurchaseOrders
                .AsNoTracking()
                .Where(o =>
                    o.TenantId == tenantId
                    && o.SupplierId == partyId
                    && o.IsReceived
                    && o.AmountPaidPaisa < o.NetPayablePaisa)
                .OrderBy(o => o.CreatedAt)
                .Select(o => new OpenPartySlipDto
                {
                    InvoiceNo = null,
                    PurchaseOrderId = o.Id,
                    Reference = o.SupplierInvoiceNo,
                    CreatedAt = o.CreatedAt,
                    TotalPaisa = o.NetPayablePaisa,
                    AmountPaidPaisa = o.AmountPaidPaisa,
                    OutstandingPaisa = o.NetPayablePaisa - o.AmountPaidPaisa
                })
                .ToListAsync(cancellationToken);
        }

        return [];
    }

    public async Task<PartyFinanceKpisDto> GetPartyFinanceKpisAsync(
        string role,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        string roleNorm = PartyTypes.Normalize(role);
        if (!PartyTypes.IsKnown(roleNorm))
        {
            throw new ArgumentException("Role must be CUSTOMER or SUPPLIER.", nameof(role));
        }

        Guid tenantId = _tenantService.TenantId;
        DateTime todayStart = DateTime.UtcNow.Date;
        DateTime todayEnd = todayStart.AddDays(1);
        DateTime overdueCutoff = DateTime.UtcNow.AddDays(-30);
        DateTime weekCutoff = DateTime.UtcNow.AddDays(-7);

        List<Party> parties = await _context.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.PartyType == roleNorm)
            .ToListAsync(cancellationToken);

        long duePositive = parties.Where(p => p.CurrentBalancePaisa > 0).Sum(p => p.CurrentBalancePaisa);
        long advance = parties.Where(p => p.CurrentBalancePaisa < 0).Sum(p => -p.CurrentBalancePaisa);

        long todayPayments = await _context.PartyLedgers
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.Type == "PAYMENT"
                && e.CreatedAt >= todayStart
                && e.CreatedAt < todayEnd
                && _context.Parties.Any(p =>
                    p.Id == e.PartyId
                    && p.TenantId == tenantId
                    && p.PartyType == roleNorm))
            .SumAsync(e => (long?)e.TransactionAmountPaisa, cancellationToken) ?? 0L;

        if (roleNorm == PartyTypes.Customer)
        {
            long overdue30 = await _context.SalesInvoices
                .AsNoTracking()
                .Where(i =>
                    i.TenantId == tenantId
                    && i.PaymentMethod == "CREDIT"
                    && i.AmountPaidPaisa < i.TotalAmountPaisa
                    && i.CreatedAt < overdueCutoff
                    && i.CustomerId != null)
                .SumAsync(
                    i => (long?)(i.TotalAmountPaisa - i.AmountPaidPaisa),
                    cancellationToken) ?? 0L;

            return new PartyFinanceKpisDto
            {
                Role = roleNorm,
                TotalReceivablePaisa = duePositive,
                Overdue30PlusPaisa = overdue30,
                AdvanceReceivedPaisa = advance,
                TodayCollectionPaisa = todayPayments
            };
        }

        long dueThisWeek = await _context.PurchaseOrders
            .AsNoTracking()
            .Where(o =>
                o.TenantId == tenantId
                && o.IsReceived
                && o.AmountPaidPaisa < o.NetPayablePaisa
                && o.CreatedAt >= weekCutoff)
            .SumAsync(
                o => (long?)(o.NetPayablePaisa - o.AmountPaidPaisa),
                cancellationToken) ?? 0L;

        return new PartyFinanceKpisDto
        {
            Role = roleNorm,
            TotalPayablePaisa = duePositive,
            DueThisWeekPaisa = dueThisWeek,
            AdvancePaidPaisa = advance,
            TodayPaidPaisa = todayPayments
        };
    }

    public async Task<PartyAgeingDto> GetAgeingAsync(
        Guid partyId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        IReadOnlyList<OpenPartySlipDto> slips = await ListOpenSlipsAsync(partyId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;

        long b0 = 0, b31 = 0, b61 = 0, b90 = 0;
        foreach (OpenPartySlipDto slip in slips)
        {
            int ageDays = (int)(now - slip.CreatedAt).TotalDays;
            if (ageDays <= 30)
            {
                b0 += slip.OutstandingPaisa;
            }
            else if (ageDays <= 60)
            {
                b31 += slip.OutstandingPaisa;
            }
            else if (ageDays <= 90)
            {
                b61 += slip.OutstandingPaisa;
            }
            else
            {
                b90 += slip.OutstandingPaisa;
            }
        }

        return new PartyAgeingDto
        {
            PartyId = partyId,
            Bucket0To30Paisa = b0,
            Bucket31To60Paisa = b31,
            Bucket61To90Paisa = b61,
            Bucket90PlusPaisa = b90
        };
    }

    public async Task<IReadOnlyList<RecentPartyPaymentDto>> ListRecentPaymentsAsync(
        string role,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        string roleNorm = PartyTypes.Normalize(role);
        if (!PartyTypes.IsKnown(roleNorm))
        {
            throw new ArgumentException("Role must be CUSTOMER or SUPPLIER.", nameof(role));
        }

        Guid tenantId = _tenantService.TenantId;
        IQueryable<PartyLedger> query =
            from e in _context.PartyLedgers.AsNoTracking()
            join p in _context.Parties.AsNoTracking() on e.PartyId equals p.Id
            where e.TenantId == tenantId
                  && p.TenantId == tenantId
                  && p.PartyType == roleNorm
                  && e.Type == "PAYMENT"
            select e;

        if (from is DateTimeOffset fromValue)
        {
            DateTime fromUtc = fromValue.UtcDateTime;
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }

        if (to is DateTimeOffset toValue)
        {
            DateTime toUtc = toValue.UtcDateTime;
            query = query.Where(e => e.CreatedAt <= toUtc);
        }

        string? searchTrim = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (searchTrim is not null)
        {
            string pattern = $"%{searchTrim}%";
            query =
                from e in query
                join p in _context.Parties.AsNoTracking() on e.PartyId equals p.Id
                where EF.Functions.ILike(p.Name, pattern)
                      || EF.Functions.ILike(e.ReferenceDetails, pattern)
                      || EF.Functions.ILike(e.PaymentMethod, pattern)
                select e;
        }

        List<PartyLedger> entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Take(100)
            .ToListAsync(cancellationToken);

        HashSet<Guid> partyIds = entries.Select(e => e.PartyId).ToHashSet();
        Dictionary<Guid, Party> partyMap = await _context.Parties
            .AsNoTracking()
            .Where(p => partyIds.Contains(p.Id) && p.TenantId == tenantId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return entries
            .Where(e => partyMap.ContainsKey(e.PartyId))
            .Select(e =>
            {
                Party party = partyMap[e.PartyId];
                bool isAdvance = e.ReferenceDetails.StartsWith(
                    "ADV-",
                    StringComparison.OrdinalIgnoreCase);
                return new RecentPartyPaymentDto
                {
                    Id = e.Id,
                    PartyId = e.PartyId,
                    PartyName = party.Name,
                    PartyType = party.PartyType,
                    AmountPaisa = e.TransactionAmountPaisa,
                    PaymentMethod = e.PaymentMethod,
                    ReferenceDetails = e.ReferenceDetails,
                    IsAdvance = isAdvance,
                    CreatedAt = e.CreatedAt
                };
            })
            .ToList();
    }

    public async Task<PartyLedgerDashboardDto> GetLedgerDashboardAsync(
        Guid partyId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (partyId == Guid.Empty)
        {
            throw new ArgumentException("Party id is required.", nameof(partyId));
        }

        if (to < from)
        {
            throw new ArgumentException("End date must be on or after start date.");
        }

        Party party = await LoadPartyAsync(partyId, cancellationToken);
        DateTime fromUtc = from.UtcDateTime;
        DateTime toUtc = to.UtcDateTime;

        long openingBalance = await _context.PartyLedgers
            .AsNoTracking()
            .Where(e =>
                e.PartyId == partyId
                && e.TenantId == _tenantService.TenantId
                && e.CreatedAt < fromUtc)
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Select(e => (long?)e.NewBalancePaisa)
            .FirstOrDefaultAsync(cancellationToken) ?? 0L;

        List<PartyLedger> entries = await _context.PartyLedgers
            .AsNoTracking()
            .Where(e =>
                e.PartyId == partyId
                && e.TenantId == _tenantService.TenantId
                && e.CreatedAt >= fromUtc
                && e.CreatedAt <= toUtc)
            .OrderBy(e => e.CreatedAt)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        List<PartyLedgerEntryDto> mapped = entries
            .Select(e => MapEntry(e, party.PartyType))
            .ToList();

        bool isCustomer = string.Equals(
            party.PartyType,
            PartyTypes.Customer,
            StringComparison.OrdinalIgnoreCase);

        long salesOrPurchases = entries
            .Where(e => e.Type == (isCustomer ? "SALE" : "PURCHASE"))
            .Sum(e => e.TransactionAmountPaisa);
        long paid = entries.Where(e => e.Type == "PAYMENT").Sum(e => e.TransactionAmountPaisa);
        long returns = entries
            .Where(e => e.Type == (isCustomer ? "CUSTOMER_RETURN" : "SUPPLIER_RETURN"))
            .Sum(e => e.TransactionAmountPaisa);
        long discounts = entries
            .Where(e =>
                e.Type == "ADJUSTMENT"
                && e.ReferenceDetails.Contains("DISCOUNT", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.TransactionAmountPaisa);
        long adjustments = entries
            .Where(e =>
                e.Type == "ADJUSTMENT"
                && !e.ReferenceDetails.Contains("DISCOUNT", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.TransactionAmountPaisa);

        long closing = mapped.Count > 0 ? mapped[^1].NewBalancePaisa : openingBalance;

        return new PartyLedgerDashboardDto
        {
            PartyId = party.Id,
            PartyName = party.Name,
            PartyType = party.PartyType,
            OpeningBalancePaisa = openingBalance,
            SalesOrPurchasesPaisa = salesOrPurchases,
            PaidPaisa = paid,
            ReturnsPaisa = returns,
            DiscountsPaisa = discounts,
            AdjustmentsPaisa = adjustments,
            ClosingBalancePaisa = closing,
            Entries = mapped
        };
    }

    private async Task<Party> LoadPartyAsync(Guid partyId, CancellationToken cancellationToken)
    {
        return await _context.Parties.AsNoTracking().FirstOrDefaultAsync(
            p => p.Id == partyId && p.TenantId == _tenantService.TenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Party profile not found in system storage.");
    }

    private static PartyLedgerEntryDto MapEntry(PartyLedger e, string partyType) =>
        new()
        {
            Id = e.Id,
            PartyId = e.PartyId,
            Type = e.Type,
            ReferenceType = PartyLedgerReferenceTypes.FromStored(e.Type, partyType),
            PaymentMethod = e.PaymentMethod,
            OldBalancePaisa = e.OldBalancePaisa,
            TransactionAmountPaisa = e.TransactionAmountPaisa,
            DebitPaisa = e.DebitPaisa,
            CreditPaisa = e.CreditPaisa,
            NewBalancePaisa = e.NewBalancePaisa,
            InvoiceNo = e.InvoiceNo,
            PurchaseOrderId = e.PurchaseOrderId,
            ReferenceDetails = e.ReferenceDetails,
            CreatedAt = e.CreatedAt
        };

    private static (long Debit, long Credit) ResolveDebitCredit(
        long oldBalance,
        long newBalance,
        long amountPaisa)
    {
        if (amountPaisa <= 0 || oldBalance == newBalance)
        {
            return (0, 0);
        }

        return newBalance > oldBalance
            ? (amountPaisa, 0)
            : (0, amountPaisa);
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
