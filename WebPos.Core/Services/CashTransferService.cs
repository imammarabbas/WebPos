using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class CashTransferService : ICashTransferService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly ITenantService _tenantService;

    public CashTransferService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
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

            CashAccount from = await _ambient.Required.CashAccounts.FirstOrDefaultAsync(
                a => a.Id == fromAccountId && a.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Source cash account was not found.");

            CashAccount to = await _ambient.Required.CashAccounts.FirstOrDefaultAsync(
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

            if (from.Type != CashAccountType.Till)
            {
                long gl = await SumGlBalanceAsync(fromCode, ct);
                CashSpendable.EnsureCanSpend(
                    fromCode,
                    CashSpendable.ForNonTill(gl),
                    amountPaisa);
            }

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

    public Task<IReadOnlyList<CashTransferDto>> ListTransfersAsync(
        Guid? accountId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;
        int take = Math.Clamp(limit, 1, 200);

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            string? filterCode = null;
            if (accountId is Guid id)
            {
                CashAccount account = await context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                    a => a.Id == id && a.TenantId == tenantId,
                    ct)
                    ?? throw new KeyNotFoundException("Cash account was not found.");
                filterCode = account.AccountCode;
            }

            IQueryable<GeneralLedgerEntry> baseQuery = context.GeneralLedgerEntries
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
                .ToListAsync(ct);

            if (groupKeys.Count == 0)
            {
                return (IReadOnlyList<CashTransferDto>)[];
            }

            List<Guid> groupIds = groupKeys.Select(g => g.Id).ToList();

            List<GeneralLedgerEntry> legs = await context.GeneralLedgerEntries
                .AsNoTracking()
                .Where(e =>
                    e.TenantId == tenantId
                    && e.TransactionType == "CASH_TRANSFER"
                    && groupIds.Contains(e.TransactionGroupId))
                .ToListAsync(ct);

            Dictionary<string, CashAccount> accountsByCode = await context.CashAccounts
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .ToDictionaryAsync(
                    a => a.AccountCode.ToUpperInvariant(),
                    StringComparer.Ordinal,
                    ct);

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

            return (IReadOnlyList<CashTransferDto>)results
                .OrderByDescending(r => r.CreatedAt)
                .Take(take)
                .ToList();
        }, cancellationToken);
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
                string fromCode = CashAccountService.SanitizeAccountCode(from.AccountCode);
                long gl = await SumGlBalanceAsync(fromCode, cancellationToken);
                long spendable = CashSpendable.ForTill(gl, fromShift.ExpectedCashPaisa);
                CashSpendable.EnsureCanSpend(
                    fromCode,
                    spendable,
                    amountPaisa,
                    fromShift.Id,
                    gl,
                    fromShift.ExpectedCashPaisa);

                await TillPhysicalCash.RecognizeIntoGlIfNeededAsync(
                    _transactionService,
                    gl,
                    fromCode,
                    amountPaisa,
                    fromShift.Id,
                    $"XFER-{fromShift.Id:N}"[..18],
                    cancellationToken);

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
            bool exists = await _ambient.Required.CashierShifts.AnyAsync(
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
                _ambient.Required,
                id,
                tenantId,
                cancellationToken);

            return locked
                ?? throw new KeyNotFoundException(
                    "Open shift was not found for the till transfer.");
        }

        CashierShift? open = await CashierShiftLocking.LockOpenByTerminalForUpdateAsync(
            _ambient.Required,
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
            _ambient.Required,
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
            CashierShift shift = await _ambient.Required.CashierShifts.AsNoTracking().FirstOrDefaultAsync(
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

        return await _ambient.Required.CashierShifts.AsNoTracking().FirstOrDefaultAsync(
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

    public Task<TillShortageResult> RecordTillShortageAsync(
        RecordTillShortageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.ShortagePaisa <= 0)
        {
            throw new ArgumentException(
                "Shortage amount must be greater than zero Paisa.",
                nameof(request));
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            CashAccount till = await _ambient.Required.CashAccounts.FirstOrDefaultAsync(
                a => a.Id == request.TillCashAccountId && a.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Till cash account was not found.");

            if (till.Type != CashAccountType.Till)
            {
                throw new InvalidOperationException("Cash shortage posting requires a till account.");
            }

            if (till.TerminalId is not Guid terminalId)
            {
                throw new InvalidOperationException(
                    $"Till account '{till.Name}' is missing a terminal link.");
            }

            CashierShift shift = await LockOpenTillShiftAsync(till, request.ShiftId, ct);
            ValidateLockedTillShift(shift, till, request.ShiftId);

            string tillCode = CashAccountService.SanitizeAccountCode(till.AccountCode);
            long gl = await SumGlBalanceAsync(tillCode, ct);
            long gap = Math.Max(0L, gl - shift.ExpectedCashPaisa);
            if (request.ShortagePaisa > gap)
            {
                throw new InvalidOperationException(
                    $"Shortage cannot exceed ledger-over-drawer gap. " +
                    $"Gap {FormatMoney.RsAndPaisa(gap)}, requested {FormatMoney.RsAndPaisa(request.ShortagePaisa)}.");
            }

            string referenceNo = $"SHORT-{Guid.NewGuid():N}"[..14];
            string details = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Cash shortage — till ledger vs drawer (shift {shift.Id:N})"
                : request.Notes.Trim();

            string expenseAccount = LedgerAccounts.Expense(ExpenseCategories.CashShortage);
            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "CASH_SHORTAGE",
                    ReferenceNo = referenceNo,
                    ReferenceDetails = details,
                    ShiftId = shift.Id,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = expenseAccount,
                            DebitPaisa = request.ShortagePaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = tillCode,
                            DebitPaisa = 0,
                            CreditPaisa = request.ShortagePaisa
                        }
                    ]
                },
                ct);

            if (request.LoggedByUserId is Guid userId && userId != Guid.Empty)
            {
                _ambient.Required.ShiftExpenses.Add(new ShiftExpense
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ShiftId = shift.Id,
                    VoucherNo = referenceNo,
                    Description = details,
                    AmountPaisa = request.ShortagePaisa,
                    ExpenseCategory = ExpenseCategories.CashShortage,
                    ReceiptReference = string.Empty,
                    PaymentMethod = "CASH",
                    IsRecurring = false,
                    LoggedByUserId = userId,
                    LoggedAt = DateTimeOffset.UtcNow
                });
            }

            Guid movementId = Guid.NewGuid();
            _ambient.Required.ShiftCashMovements.Add(new ShiftCashMovement
            {
                Id = movementId,
                TenantId = tenantId,
                ShiftId = shift.Id,
                TillCashAccountId = till.Id,
                Direction = ShiftCashMovementDirections.Out,
                AmountPaisa = request.ShortagePaisa,
                AlignPaisa = 0,
                ExcessPaisa = 0,
                Reason = "Cash Shortage",
                Note = details,
                Status = ShiftCashMovementStatuses.Unreconciled,
                TransactionGroupId = transactionGroupId,
                CreatedByUserId = request.LoggedByUserId ?? Guid.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });

            long glAfter = gl - request.ShortagePaisa;
            return new TillShortageResult
            {
                TransactionGroupId = transactionGroupId,
                ReferenceNo = referenceNo,
                ShortagePaisa = request.ShortagePaisa,
                TillGlBalanceAfterPaisa = glAfter,
                MovementId = movementId
            };
        }, cancellationToken);
    }

    public Task<TillCashMovementResult> RecordTillCashInAsync(
        RecordTillCashInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Cash In amount must be greater than zero Paisa.", nameof(request));
        }

        string reason = string.IsNullOrWhiteSpace(request.Reason)
            ? ShiftCashInReasons.UnregisteredCash
            : request.Reason.Trim();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            CashAccount till = await RequireTillAsync(request.TillCashAccountId, tenantId, ct);
            CashierShift shift = await LockOpenTillShiftAsync(till, request.ShiftId, ct);
            ValidateLockedTillShift(shift, till, request.ShiftId);

            string tillCode = CashAccountService.SanitizeAccountCode(till.AccountCode);
            long gl = await SumGlBalanceAsync(tillCode, ct);
            long gap = Math.Max(0L, gl - shift.ExpectedCashPaisa);
            long align = Math.Min(request.AmountPaisa, gap);
            long excess = request.AmountPaisa - align;

            Guid? transactionGroupId = null;
            if (excess > 0)
            {
                string referenceNo = $"CIN-{Guid.NewGuid():N}"[..14];
                string details = string.IsNullOrWhiteSpace(request.Note)
                    ? $"Cash In — {reason} (shift {shift.Id:N})"
                    : request.Note.Trim();

                transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                    new DoubleEntryPostRequest
                    {
                        TransactionType = "CASH_IN",
                        ReferenceNo = referenceNo,
                        ReferenceDetails = details,
                        ShiftId = shift.Id,
                        Postings =
                        [
                            new LedgerPosting
                            {
                                AccountCode = tillCode,
                                DebitPaisa = excess,
                                CreditPaisa = 0
                            },
                            new LedgerPosting
                            {
                                AccountCode = LedgerAccounts.UnregisteredCash,
                                DebitPaisa = 0,
                                CreditPaisa = excess
                            }
                        ]
                    },
                    ct);
            }

            shift.ExpectedCashPaisa += request.AmountPaisa;

            Guid movementId = Guid.NewGuid();
            _ambient.Required.ShiftCashMovements.Add(new ShiftCashMovement
            {
                Id = movementId,
                TenantId = tenantId,
                ShiftId = shift.Id,
                TillCashAccountId = till.Id,
                Direction = ShiftCashMovementDirections.In,
                AmountPaisa = request.AmountPaisa,
                AlignPaisa = align,
                ExcessPaisa = excess,
                Reason = reason,
                Note = request.Note?.Trim() ?? string.Empty,
                Status = ShiftCashMovementStatuses.Unreconciled,
                TransactionGroupId = transactionGroupId,
                CreatedByUserId = request.CreatedByUserId ?? Guid.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });

            long glAfter = gl + excess;
            return new TillCashMovementResult
            {
                MovementId = movementId,
                Direction = ShiftCashMovementDirections.In,
                AmountPaisa = request.AmountPaisa,
                AlignPaisa = align,
                ExcessPaisa = excess,
                Status = ShiftCashMovementStatuses.Unreconciled,
                ExpectedCashAfterPaisa = shift.ExpectedCashPaisa,
                TillGlBalanceAfterPaisa = glAfter,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);
    }

    public Task<TillCashMovementResult> RecordTillCashOutAsync(
        RecordTillCashOutRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Cash Out amount must be greater than zero Paisa.", nameof(request));
        }

        string reason = string.IsNullOrWhiteSpace(request.Reason)
            ? ShiftCashOutReasons.UnregisteredPayment
            : request.Reason.Trim();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            CashAccount till = await RequireTillAsync(request.TillCashAccountId, tenantId, ct);
            CashierShift shift = await LockOpenTillShiftAsync(till, request.ShiftId, ct);
            ValidateLockedTillShift(shift, till, request.ShiftId);

            string tillCode = CashAccountService.SanitizeAccountCode(till.AccountCode);
            long gl = await SumGlBalanceAsync(tillCode, ct);
            long spendable = CashSpendable.ForTill(gl, shift.ExpectedCashPaisa);
            CashSpendable.EnsureCanSpend(tillCode, spendable, request.AmountPaisa);

            if (request.AmountPaisa > shift.ExpectedCashPaisa)
            {
                throw new InsufficientCashBalanceException(
                    tillCode,
                    shift.ExpectedCashPaisa,
                    request.AmountPaisa);
            }

            string referenceNo = $"COUT-{Guid.NewGuid():N}"[..14];
            await TillPhysicalCash.RecognizeIntoGlIfNeededAsync(
                _transactionService,
                gl,
                tillCode,
                request.AmountPaisa,
                shift.Id,
                referenceNo,
                ct);
            string details = string.IsNullOrWhiteSpace(request.Note)
                ? $"Cash Out — {reason} (shift {shift.Id:N})"
                : request.Note.Trim();

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "CASH_OUT",
                    ReferenceNo = referenceNo,
                    ReferenceDetails = details,
                    ShiftId = shift.Id,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.UnregisteredCash,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = tillCode,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                },
                ct);

            shift.ExpectedCashPaisa -= request.AmountPaisa;

            Guid movementId = Guid.NewGuid();
            _ambient.Required.ShiftCashMovements.Add(new ShiftCashMovement
            {
                Id = movementId,
                TenantId = tenantId,
                ShiftId = shift.Id,
                TillCashAccountId = till.Id,
                Direction = ShiftCashMovementDirections.Out,
                AmountPaisa = request.AmountPaisa,
                AlignPaisa = 0,
                ExcessPaisa = request.AmountPaisa,
                Reason = reason,
                Note = request.Note?.Trim() ?? string.Empty,
                Status = ShiftCashMovementStatuses.Unreconciled,
                TransactionGroupId = transactionGroupId,
                CreatedByUserId = request.CreatedByUserId ?? Guid.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });

            return new TillCashMovementResult
            {
                MovementId = movementId,
                Direction = ShiftCashMovementDirections.Out,
                AmountPaisa = request.AmountPaisa,
                AlignPaisa = 0,
                ExcessPaisa = request.AmountPaisa,
                Status = ShiftCashMovementStatuses.Unreconciled,
                ExpectedCashAfterPaisa = shift.ExpectedCashPaisa,
                TillGlBalanceAfterPaisa = gl - request.AmountPaisa,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);
    }

    public Task<TillCashMovementResult> ReconcileTillCashMovementAsync(
        ReconcileTillCashMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (string.IsNullOrWhiteSpace(request.LinkedReferenceType) || request.LinkedReferenceId == Guid.Empty)
        {
            throw new ArgumentException("Linked reference type and id are required.");
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            ShiftCashMovement movement = await _ambient.Required.ShiftCashMovements.FirstOrDefaultAsync(
                m => m.Id == request.MovementId && m.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Cash movement was not found.");

            if (!string.Equals(movement.Status, ShiftCashMovementStatuses.Unreconciled, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Movement is {movement.Status}; only UNRECONCILED can be linked.");
            }

            CashAccount till = await RequireTillAsync(movement.TillCashAccountId, tenantId, ct);
            CashierShift shift = await LockOpenTillShiftAsync(till, movement.ShiftId, ct);
            string tillCode = CashAccountService.SanitizeAccountCode(till.AccountCode);
            long gl = await SumGlBalanceAsync(tillCode, ct);

            Guid? reverseGroupId = null;

            // Reverse only GL that Cash In/Out actually posted (excess), then linked txn posts normally.
            if (movement.ExcessPaisa > 0
                && string.Equals(movement.Direction, ShiftCashMovementDirections.In, StringComparison.OrdinalIgnoreCase))
            {
                reverseGroupId = await _transactionService.PostBalancedEntriesAsync(
                    new DoubleEntryPostRequest
                    {
                        TransactionType = "CASH_IN_REVERSAL",
                        ReferenceNo = $"CINR-{Guid.NewGuid():N}"[..14],
                        ReferenceDetails = $"Reconcile Cash In {movement.Id:N}",
                        ShiftId = shift.Id,
                        Postings =
                        [
                            new LedgerPosting
                            {
                                AccountCode = LedgerAccounts.UnregisteredCash,
                                DebitPaisa = movement.ExcessPaisa,
                                CreditPaisa = 0
                            },
                            new LedgerPosting
                            {
                                AccountCode = tillCode,
                                DebitPaisa = 0,
                                CreditPaisa = movement.ExcessPaisa
                            }
                        ]
                    },
                    ct);
                shift.ExpectedCashPaisa -= movement.ExcessPaisa;
                gl -= movement.ExcessPaisa;
            }
            else if (movement.ExcessPaisa > 0
                     && string.Equals(movement.Direction, ShiftCashMovementDirections.Out, StringComparison.OrdinalIgnoreCase))
            {
                reverseGroupId = await _transactionService.PostBalancedEntriesAsync(
                    new DoubleEntryPostRequest
                    {
                        TransactionType = "CASH_OUT_REVERSAL",
                        ReferenceNo = $"COUTR-{Guid.NewGuid():N}"[..15],
                        ReferenceDetails = $"Reconcile Cash Out {movement.Id:N}",
                        ShiftId = shift.Id,
                        Postings =
                        [
                            new LedgerPosting
                            {
                                AccountCode = tillCode,
                                DebitPaisa = movement.ExcessPaisa,
                                CreditPaisa = 0
                            },
                            new LedgerPosting
                            {
                                AccountCode = LedgerAccounts.UnregisteredCash,
                                DebitPaisa = 0,
                                CreditPaisa = movement.ExcessPaisa
                            }
                        ]
                    },
                    ct);
                shift.ExpectedCashPaisa += movement.ExcessPaisa;
                gl += movement.ExcessPaisa;
            }

            // Align-only Cash In: ExpectedCash already matches registered ledger; do not reverse drawer.
            movement.Status = ShiftCashMovementStatuses.Reconciled;
            movement.LinkedReferenceType = request.LinkedReferenceType.Trim();
            movement.LinkedReferenceId = request.LinkedReferenceId;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                movement.Note = string.IsNullOrWhiteSpace(movement.Note)
                    ? request.Notes.Trim()
                    : $"{movement.Note} | {request.Notes.Trim()}";
            }

            return new TillCashMovementResult
            {
                MovementId = movement.Id,
                Direction = movement.Direction,
                AmountPaisa = movement.AmountPaisa,
                AlignPaisa = movement.AlignPaisa,
                ExcessPaisa = movement.ExcessPaisa,
                Status = movement.Status,
                ExpectedCashAfterPaisa = shift.ExpectedCashPaisa,
                TillGlBalanceAfterPaisa = gl,
                TransactionGroupId = reverseGroupId ?? movement.TransactionGroupId
            };
        }, cancellationToken);
    }

    public Task<TillCashMovementResult> ReverseTillCashMovementAsync(
        Guid movementId,
        Guid? reversedByUserId = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            ShiftCashMovement movement = await _ambient.Required.ShiftCashMovements.FirstOrDefaultAsync(
                m => m.Id == movementId && m.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Cash movement was not found.");

            if (string.Equals(movement.Status, ShiftCashMovementStatuses.Reversed, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Movement is already reversed.");
            }

            if (string.Equals(movement.Status, ShiftCashMovementStatuses.Reconciled, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Reconciled movements cannot be reversed; reverse the linked transaction instead.");
            }

            CashAccount till = await RequireTillAsync(movement.TillCashAccountId, tenantId, ct);
            CashierShift shift = await LockOpenTillShiftAsync(till, movement.ShiftId, ct);
            string tillCode = CashAccountService.SanitizeAccountCode(till.AccountCode);
            long gl = await SumGlBalanceAsync(tillCode, ct);
            Guid? reverseGroupId = null;

            bool isShortage = string.Equals(movement.Reason, "Cash Shortage", StringComparison.OrdinalIgnoreCase);

            if (isShortage)
            {
                // Shortage wrote off till GL only; ExpectedCash was unchanged — reverse GL only.
                string expenseAccount = LedgerAccounts.Expense(ExpenseCategories.CashShortage);
                reverseGroupId = await _transactionService.PostBalancedEntriesAsync(
                    new DoubleEntryPostRequest
                    {
                        TransactionType = "CASH_SHORTAGE_REVERSAL",
                        ReferenceNo = $"SHORTR-{Guid.NewGuid():N}"[..16],
                        ReferenceDetails = notes?.Trim() ?? $"Reverse shortage {movement.Id:N}",
                        ShiftId = shift.Id,
                        Postings =
                        [
                            new LedgerPosting
                            {
                                AccountCode = tillCode,
                                DebitPaisa = movement.AmountPaisa,
                                CreditPaisa = 0
                            },
                            new LedgerPosting
                            {
                                AccountCode = expenseAccount,
                                DebitPaisa = 0,
                                CreditPaisa = movement.AmountPaisa
                            }
                        ]
                    },
                    ct);
                gl += movement.AmountPaisa;
            }
            else if (string.Equals(movement.Direction, ShiftCashMovementDirections.In, StringComparison.OrdinalIgnoreCase))
            {
                if (movement.ExcessPaisa > 0)
                {
                    reverseGroupId = await _transactionService.PostBalancedEntriesAsync(
                        new DoubleEntryPostRequest
                        {
                            TransactionType = "CASH_IN_REVERSAL",
                            ReferenceNo = $"CINR-{Guid.NewGuid():N}"[..14],
                            ReferenceDetails = notes?.Trim() ?? $"Reverse Cash In {movement.Id:N}",
                            ShiftId = shift.Id,
                            Postings =
                            [
                                new LedgerPosting
                                {
                                    AccountCode = LedgerAccounts.UnregisteredCash,
                                    DebitPaisa = movement.ExcessPaisa,
                                    CreditPaisa = 0
                                },
                                new LedgerPosting
                                {
                                    AccountCode = tillCode,
                                    DebitPaisa = 0,
                                    CreditPaisa = movement.ExcessPaisa
                                }
                            ]
                        },
                        ct);
                    gl -= movement.ExcessPaisa;
                }

                shift.ExpectedCashPaisa -= movement.AmountPaisa;
            }
            else
            {
                if (movement.ExcessPaisa > 0)
                {
                    reverseGroupId = await _transactionService.PostBalancedEntriesAsync(
                        new DoubleEntryPostRequest
                        {
                            TransactionType = "CASH_OUT_REVERSAL",
                            ReferenceNo = $"COUTR-{Guid.NewGuid():N}"[..15],
                            ReferenceDetails = notes?.Trim() ?? $"Reverse Cash Out {movement.Id:N}",
                            ShiftId = shift.Id,
                            Postings =
                            [
                                new LedgerPosting
                                {
                                    AccountCode = tillCode,
                                    DebitPaisa = movement.ExcessPaisa,
                                    CreditPaisa = 0
                                },
                                new LedgerPosting
                                {
                                    AccountCode = LedgerAccounts.UnregisteredCash,
                                    DebitPaisa = 0,
                                    CreditPaisa = movement.ExcessPaisa
                                }
                            ]
                        },
                        ct);
                    gl += movement.ExcessPaisa;
                }

                shift.ExpectedCashPaisa += movement.AmountPaisa;
            }

            if (shift.ExpectedCashPaisa < 0)
            {
                throw new InvalidOperationException("Reverse would drive ExpectedCash negative.");
            }

            movement.Status = ShiftCashMovementStatuses.Reversed;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                movement.Note = string.IsNullOrWhiteSpace(movement.Note)
                    ? notes.Trim()
                    : $"{movement.Note} | REV: {notes.Trim()}";
            }

            _ = reversedByUserId;

            return new TillCashMovementResult
            {
                MovementId = movement.Id,
                Direction = movement.Direction,
                AmountPaisa = movement.AmountPaisa,
                AlignPaisa = movement.AlignPaisa,
                ExcessPaisa = movement.ExcessPaisa,
                Status = movement.Status,
                ExpectedCashAfterPaisa = shift.ExpectedCashPaisa,
                TillGlBalanceAfterPaisa = gl,
                TransactionGroupId = reverseGroupId ?? movement.TransactionGroupId
            };
        }, cancellationToken);
    }

    public Task<CapitalFundingResult> RecordOwnerInvestmentAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default) =>
        RecordCapitalInflowAsync(
            request,
            transactionType: "OWNER_INVESTMENT",
            offsetAccountCode: LedgerAccounts.OwnerCapital,
            referencePrefix: "OINV",
            cancellationToken);

    public Task<CapitalFundingResult> RecordLoanReceivedAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default) =>
        RecordCapitalInflowAsync(
            request,
            transactionType: "LOAN_RECEIVED",
            offsetAccountCode: LedgerAccounts.LoanPayable,
            referencePrefix: "LOANIN",
            cancellationToken);

    public Task<CapitalFundingResult> RecordLoanRepaymentAsync(
        CapitalFundingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero Paisa.", nameof(request));
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            CashAccount account = await RequireCashAccountAsync(request.CashAccountId, tenantId, ct);
            string code = CashAccountService.SanitizeAccountCode(account.AccountCode);

            long gl = await SumGlBalanceAsync(code, ct);
            CashierShift? shift = null;
            long spendable;
            if (account.Type == CashAccountType.Till)
            {
                shift = await LockOpenTillShiftAsync(account, request.ShiftId, ct);
                if (request.ShiftId is Guid sid && sid != Guid.Empty)
                {
                    ValidateLockedTillShift(shift, account, sid);
                }

                spendable = CashSpendable.ForTill(gl, shift.ExpectedCashPaisa);
                CashSpendable.EnsureCanSpend(code, spendable, request.AmountPaisa);
                if (request.AmountPaisa > shift.ExpectedCashPaisa)
                {
                    throw new InsufficientCashBalanceException(code, shift.ExpectedCashPaisa, request.AmountPaisa);
                }

                await TillPhysicalCash.RecognizeIntoGlIfNeededAsync(
                    _transactionService,
                    gl,
                    code,
                    request.AmountPaisa,
                    shift.Id,
                    $"LOANOUT-{shift.Id:N}"[..16],
                    ct);
            }
            else
            {
                spendable = CashSpendable.ForNonTill(gl);
                CashSpendable.EnsureCanSpend(code, spendable, request.AmountPaisa);
            }

            string referenceNo = $"LOANOUT-{Guid.NewGuid():N}"[..16];
            string details = string.IsNullOrWhiteSpace(request.Note)
                ? $"Loan repayment from {account.Name}"
                : request.Note.Trim();

            Guid groupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = "LOAN_REPAYMENT",
                    ReferenceNo = referenceNo,
                    ReferenceDetails = details,
                    ShiftId = shift?.Id,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = LedgerAccounts.LoanPayable,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = code,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                },
                ct);

            long? expectedAfter = null;
            if (shift is not null)
            {
                shift.ExpectedCashPaisa -= request.AmountPaisa;
                expectedAfter = shift.ExpectedCashPaisa;
            }

            return new CapitalFundingResult
            {
                TransactionGroupId = groupId,
                ReferenceNo = referenceNo,
                TransactionType = "LOAN_REPAYMENT",
                CashAccountId = account.Id,
                AmountPaisa = request.AmountPaisa,
                AccountGlBalanceAfterPaisa = gl - request.AmountPaisa,
                ExpectedCashAfterPaisa = expectedAfter
            };
        }, cancellationToken);
    }

    private Task<CapitalFundingResult> RecordCapitalInflowAsync(
        CapitalFundingRequest request,
        string transactionType,
        string offsetAccountCode,
        string referencePrefix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero Paisa.", nameof(request));
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            Guid tenantId = _tenantService.TenantId;
            CashAccount account = await RequireCashAccountAsync(request.CashAccountId, tenantId, ct);
            string code = CashAccountService.SanitizeAccountCode(account.AccountCode);

            CashierShift? shift = null;
            if (account.Type == CashAccountType.Till)
            {
                shift = await LockOpenTillShiftAsync(account, request.ShiftId, ct);
                if (request.ShiftId is Guid sid && sid != Guid.Empty)
                {
                    ValidateLockedTillShift(shift, account, sid);
                }
            }

            string referenceNo = $"{referencePrefix}-{Guid.NewGuid():N}"[..16];
            string details = string.IsNullOrWhiteSpace(request.Note)
                ? $"{transactionType} into {account.Name}"
                : request.Note.Trim();

            Guid groupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = transactionType,
                    ReferenceNo = referenceNo,
                    ReferenceDetails = details,
                    ShiftId = shift?.Id,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = code,
                            DebitPaisa = request.AmountPaisa,
                            CreditPaisa = 0
                        },
                        new LedgerPosting
                        {
                            AccountCode = offsetAccountCode,
                            DebitPaisa = 0,
                            CreditPaisa = request.AmountPaisa
                        }
                    ]
                },
                ct);

            long glAfter = await SumGlBalanceAsync(code, ct);
            long? expectedAfter = null;
            if (shift is not null)
            {
                shift.ExpectedCashPaisa += request.AmountPaisa;
                expectedAfter = shift.ExpectedCashPaisa;
            }

            return new CapitalFundingResult
            {
                TransactionGroupId = groupId,
                ReferenceNo = referenceNo,
                TransactionType = transactionType,
                CashAccountId = account.Id,
                AmountPaisa = request.AmountPaisa,
                AccountGlBalanceAfterPaisa = glAfter,
                ExpectedCashAfterPaisa = expectedAfter
            };
        }, cancellationToken);
    }

    private async Task<CashAccount> RequireCashAccountAsync(
        Guid cashAccountId,
        Guid tenantId,
        CancellationToken ct)
    {
        return await _ambient.Required.CashAccounts.FirstOrDefaultAsync(
            a => a.Id == cashAccountId && a.TenantId == tenantId,
            ct)
            ?? throw new KeyNotFoundException("Cash account was not found.");
    }

    public Task<IReadOnlyList<TillCashMovementDto>> ListTillCashMovementsAsync(
        Guid? shiftId = null,
        Guid? tillCashAccountId = null,
        bool unreconciledOnly = false,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        int clamped = Math.Clamp(limit, 1, 500);
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            IQueryable<ShiftCashMovement> query = context.ShiftCashMovements.AsNoTracking()
                .Where(m => m.TenantId == tenantId);

            if (shiftId is Guid sid)
            {
                query = query.Where(m => m.ShiftId == sid);
            }

            if (tillCashAccountId is Guid tid)
            {
                query = query.Where(m => m.TillCashAccountId == tid);
            }

            if (unreconciledOnly)
            {
                query = query.Where(m => m.Status == ShiftCashMovementStatuses.Unreconciled);
            }

            List<ShiftCashMovement> rows = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(clamped)
                .ToListAsync(ct);

            return (IReadOnlyList<TillCashMovementDto>)rows.Select(MapMovement).ToList();
        }, cancellationToken);
    }

    private async Task<CashAccount> RequireTillAsync(
        Guid tillCashAccountId,
        Guid tenantId,
        CancellationToken ct)
    {
        CashAccount till = await _ambient.Required.CashAccounts.FirstOrDefaultAsync(
            a => a.Id == tillCashAccountId && a.TenantId == tenantId,
            ct)
            ?? throw new KeyNotFoundException("Till cash account was not found.");

        if (till.Type != CashAccountType.Till)
        {
            throw new InvalidOperationException("Cash In/Out requires a till account.");
        }

        if (till.TerminalId is null)
        {
            throw new InvalidOperationException($"Till account '{till.Name}' is missing a terminal link.");
        }

        return till;
    }

    private static TillCashMovementDto MapMovement(ShiftCashMovement m) => new()
    {
        Id = m.Id,
        ShiftId = m.ShiftId,
        TillCashAccountId = m.TillCashAccountId,
        Direction = m.Direction,
        AmountPaisa = m.AmountPaisa,
        AlignPaisa = m.AlignPaisa,
        ExcessPaisa = m.ExcessPaisa,
        Reason = m.Reason,
        Note = m.Note,
        Status = m.Status,
        LinkedReferenceType = m.LinkedReferenceType,
        LinkedReferenceId = m.LinkedReferenceId,
        TransactionGroupId = m.TransactionGroupId,
        CreatedByUserId = m.CreatedByUserId,
        CreatedAt = m.CreatedAt
    };

    private async Task<long> SumGlBalanceAsync(string accountCode, CancellationToken cancellationToken)
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
                "Tenant context is not resolved for cash transfer operations.");
        }
    }
}
