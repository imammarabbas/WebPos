using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class CashTransferService : ICashTransferService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;
    private readonly ITenantService _tenantService;

    public CashTransferService(
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

    public Task<CashTransferResult> TransferAsync(
        Guid fromAccountId,
        Guid toAccountId,
        long amountPaisa,
        string? notes,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (amountPaisa <= 0)
        {
            throw new ArgumentException(
                "Transfer amount must be greater than zero Paisa.",
                nameof(amountPaisa));
        }

        if (fromAccountId == Guid.Empty || toAccountId == Guid.Empty)
        {
            throw new ArgumentException("From and to cash accounts are required.");
        }

        if (fromAccountId == toAccountId)
        {
            throw new InvalidOperationException("Cannot transfer to the same cash account.");
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;

            CashAccount from = await _context.CashAccounts.FirstOrDefaultAsync(
                a => a.Id == fromAccountId && a.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Source cash account was not found.");

            CashAccount to = await _context.CashAccounts.FirstOrDefaultAsync(
                a => a.Id == toAccountId && a.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Destination cash account was not found.");

            if (!from.IsActive || !to.IsActive)
            {
                throw new InvalidOperationException(
                    "Cannot transfer using an inactive cash account.");
            }

            string fromCode = CashAccountService.SanitizeAccountCode(from.AccountCode);
            string toCode = CashAccountService.SanitizeAccountCode(to.AccountCode);
            if (!string.Equals(from.AccountCode, fromCode, StringComparison.Ordinal))
            {
                from.AccountCode = fromCode;
            }

            if (!string.Equals(to.AccountCode, toCode, StringComparison.Ordinal))
            {
                to.AccountCode = toCode;
            }

            string referenceNo = $"XFER-{Guid.NewGuid():N}"[..13];
            string details = string.IsNullOrWhiteSpace(notes)
                ? $"{from.Name} → {to.Name}"
                : notes.Trim();

            Guid? resolvedShiftId = await SyncTillFloatsAsync(
                from,
                to,
                amountPaisa,
                shiftId,
                ct);

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "CASH_TRANSFER",
                    ReferenceNo = referenceNo,
                    ReferenceDetails = details,
                    ShiftId = resolvedShiftId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = toCode,
                            DebitPaisa = amountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = fromCode,
                            DebitPaisa = 0,
                            CreditPaisa = amountPaisa
                        }
                    ]
                },
                ct);

            return new CashTransferResult
            {
                TransactionGroupId = transactionGroupId,
                ReferenceNo = referenceNo,
                FromAccountId = from.Id,
                ToAccountId = to.Id,
                AmountPaisa = amountPaisa
            };
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(
        Guid? accountId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        int take = Math.Clamp(limit, 1, 200);

        string? filterCode = null;
        if (accountId is Guid id)
        {
            CashAccount account = await _context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                a => a.Id == id && a.TenantId == tenantId,
                cancellationToken)
                ?? throw new KeyNotFoundException("Cash account was not found.");
            filterCode = account.AccountCode;
        }

        IQueryable<GeneralLedgerEntry> baseQuery = _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.TransactionType == "CASH_TRANSFER");

        if (filterCode is not null)
        {
            string filterUpper = filterCode.ToUpperInvariant();
            baseQuery = baseQuery.Where(e => e.AccountCode.ToUpper() == filterUpper);
        }

        var groupKeys = await baseQuery
            .GroupBy(e => e.TransactionGroupId)
            .Select(g => new { Id = g.Key, CreatedAt = g.Max(x => x.CreatedAt) })
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (groupKeys.Count == 0)
        {
            return [];
        }

        List<Guid> groupIds = groupKeys.Select(g => g.Id).ToList();

        List<GeneralLedgerEntry> legs = await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId
                && e.TransactionType == "CASH_TRANSFER"
                && groupIds.Contains(e.TransactionGroupId))
            .ToListAsync(cancellationToken);

        Dictionary<string, CashAccount> accountsByCode = await _context.CashAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .ToDictionaryAsync(
                a => a.AccountCode.ToUpperInvariant(),
                StringComparer.Ordinal,
                cancellationToken);

        List<CashTransferDto> results = [];
        foreach (IGrouping<Guid, GeneralLedgerEntry> group in legs
                     .GroupBy(e => e.TransactionGroupId)
                     .OrderByDescending(g => g.Max(x => x.CreatedAt)))
        {
            GeneralLedgerEntry? creditLeg = group.FirstOrDefault(e => e.CreditPaisa > 0);
            GeneralLedgerEntry? debitLeg = group.FirstOrDefault(e => e.DebitPaisa > 0);
            if (creditLeg is null || debitLeg is null)
            {
                continue;
            }

            accountsByCode.TryGetValue(
                creditLeg.AccountCode.ToUpperInvariant(),
                out CashAccount? fromAccount);
            accountsByCode.TryGetValue(
                debitLeg.AccountCode.ToUpperInvariant(),
                out CashAccount? toAccount);

            results.Add(new CashTransferDto
            {
                TransactionGroupId = group.Key,
                ReferenceNo = creditLeg.ReferenceNo,
                FromAccountId = fromAccount?.Id ?? Guid.Empty,
                FromAccountName = fromAccount?.Name ?? creditLeg.AccountCode,
                FromAccountCode = creditLeg.AccountCode,
                ToAccountId = toAccount?.Id ?? Guid.Empty,
                ToAccountName = toAccount?.Name ?? debitLeg.AccountCode,
                ToAccountCode = debitLeg.AccountCode,
                AmountPaisa = creditLeg.CreditPaisa,
                Notes = creditLeg.ReferenceDetails ?? string.Empty,
                ShiftId = creditLeg.ShiftId ?? debitLeg.ShiftId,
                CreatedAt = creditLeg.CreatedAt
            });
        }

        return results
            .OrderByDescending(r => r.CreatedAt)
            .Take(take)
            .ToList();
    }

    private async Task<Guid?> SyncTillFloatsAsync(
        CashAccount from,
        CashAccount to,
        long amountPaisa,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        Guid? resolvedShiftId = shiftId;
        Guid tenantId = _tenantService.TenantId;

        // Lock open shifts in stable Id order when both sides are tills (avoid deadlocks).
        CashierShift? fromShift = null;
        CashierShift? toShift = null;

        bool fromIsTill = from.Type == CashAccountType.Till;
        bool toIsTill = to.Type == CashAccountType.Till;

        if (fromIsTill && toIsTill)
        {
            // Resolve terminal targets first (no lock yet), then lock by ascending shift id.
            CashierShift fromCandidate = await ResolveOpenTillShiftUnlockedAsync(
                from,
                shiftId,
                cancellationToken);
            CashierShift toCandidate = await ResolveOpenTillShiftUnlockedAsync(
                to,
                shiftId,
                cancellationToken);

            if (fromCandidate.Id.CompareTo(toCandidate.Id) <= 0)
            {
                fromShift = await LockShiftByIdAsync(fromCandidate.Id, tenantId, cancellationToken);
                toShift = fromCandidate.Id == toCandidate.Id
                    ? fromShift
                    : await LockShiftByIdAsync(toCandidate.Id, tenantId, cancellationToken);
            }
            else
            {
                toShift = await LockShiftByIdAsync(toCandidate.Id, tenantId, cancellationToken);
                fromShift = await LockShiftByIdAsync(fromCandidate.Id, tenantId, cancellationToken);
            }

            ValidateLockedTillShift(fromShift, from, shiftId);
            ValidateLockedTillShift(toShift, to, shiftId);
        }
        else if (fromIsTill)
        {
            fromShift = await LockOpenTillShiftAsync(from, shiftId, cancellationToken);
            ValidateLockedTillShift(fromShift, from, shiftId);
        }
        else if (toIsTill)
        {
            toShift = await LockOpenTillShiftAsync(to, shiftId, cancellationToken);
            ValidateLockedTillShift(toShift, to, shiftId);
        }

        if (fromShift is not null)
        {
            if (fromShift.ExpectedCashPaisa < amountPaisa)
            {
                throw new InsufficientTillBalanceException(
                    fromShift.Id,
                    fromShift.ExpectedCashPaisa,
                    amountPaisa);
            }

            fromShift.ExpectedCashPaisa -= amountPaisa;
            resolvedShiftId ??= fromShift.Id;
        }

        if (toShift is not null)
        {
            toShift.ExpectedCashPaisa += amountPaisa;
            resolvedShiftId ??= toShift.Id;
        }

        if (shiftId is Guid explicitShiftId
            && !fromIsTill
            && !toIsTill)
        {
            bool exists = await _context.CashierShifts.AnyAsync(
                s => s.Id == explicitShiftId && s.TenantId == tenantId,
                cancellationToken);
            if (!exists)
            {
                throw new KeyNotFoundException("Shift was not found for the transfer.");
            }
        }

        return resolvedShiftId;
    }

    private async Task<CashierShift> LockOpenTillShiftAsync(
        CashAccount tillAccount,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        if (tillAccount.TerminalId is not Guid terminalId)
        {
            throw new InvalidOperationException(
                $"Till account '{tillAccount.Name}' is missing a terminal link.");
        }

        Guid tenantId = _tenantService.TenantId;

        if (shiftId is Guid id)
        {
            CashierShift? locked = await CashierShiftLocking.LockByIdForUpdateAsync(
                _context,
                id,
                tenantId,
                cancellationToken);

            return locked
                ?? throw new KeyNotFoundException(
                    "Open shift was not found for the till transfer.");
        }

        CashierShift? open = await CashierShiftLocking.LockOpenByTerminalForUpdateAsync(
            _context,
            terminalId,
            tenantId,
            cancellationToken);

        return open
            ?? throw new InvalidOperationException(
                $"No open shift found for till '{tillAccount.Name}'. Open a shift before transferring till cash.");
    }

    private async Task<CashierShift> LockShiftByIdAsync(
        Guid shiftId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        CashierShift? locked = await CashierShiftLocking.LockByIdForUpdateAsync(
            _context,
            shiftId,
            tenantId,
            cancellationToken);

        return locked
            ?? throw new InsufficientTillBalanceException(
                $"Could not lock cashier shift {shiftId} (row missing after resolve).",
                shiftId);
    }

    /// <summary>
    /// Pre-lock resolve (no FOR UPDATE) used only to determine lock order when both sides are tills.
    /// </summary>
    private async Task<CashierShift> ResolveOpenTillShiftUnlockedAsync(
        CashAccount tillAccount,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        if (tillAccount.TerminalId is not Guid terminalId)
        {
            throw new InvalidOperationException(
                $"Till account '{tillAccount.Name}' is missing a terminal link.");
        }

        Guid tenantId = _tenantService.TenantId;

        if (shiftId is Guid id)
        {
            CashierShift shift = await _context.CashierShifts.AsNoTracking().FirstOrDefaultAsync(
                s => s.Id == id && s.TenantId == tenantId,
                cancellationToken)
                ?? throw new KeyNotFoundException("Open shift was not found for the till transfer.");

            if (!string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Till transfers require an OPEN shift.");
            }

            if (shift.TerminalId != terminalId)
            {
                throw new InvalidOperationException(
                    "Shift does not belong to the till terminal for this cash account.");
            }

            return shift;
        }

        return await _context.CashierShifts.AsNoTracking().FirstOrDefaultAsync(
                s => s.TenantId == tenantId
                     && s.TerminalId == terminalId
                     && s.Status == "OPEN",
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"No open shift found for till '{tillAccount.Name}'. Open a shift before transferring till cash.");
    }

    private static void ValidateLockedTillShift(
        CashierShift shift,
        CashAccount tillAccount,
        Guid? requestedShiftId)
    {
        if (!string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Till transfers require an OPEN shift.");
        }

        if (tillAccount.TerminalId is Guid terminalId && shift.TerminalId != terminalId)
        {
            throw new InvalidOperationException(
                "Shift does not belong to the till terminal for this cash account.");
        }

        if (requestedShiftId is Guid expectedId && shift.Id != expectedId)
        {
            throw new InvalidOperationException(
                "Locked shift does not match the requested shift id.");
        }
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for cash transfer operations.");
        }
    }
}
