using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;
using WebPos.Core.Seeding;

namespace WebPos.Core.Services;

public sealed class CashAccountService : ICashAccountService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITenantService _tenantService;

    public CashAccountService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<IReadOnlyList<CashAccountDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            IQueryable<CashAccount> query = context.CashAccounts
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId);

            if (!includeInactive)
            {
                query = query.Where(a => a.IsActive);
            }

            List<CashAccount> accounts = await query
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(ct);

            Dictionary<Guid, string> terminalNames = await LoadTerminalNamesAsync(
                context,
                accounts.Where(a => a.TerminalId.HasValue).Select(a => a.TerminalId!.Value),
                ct);

            return (IReadOnlyList<CashAccountDto>)accounts.Select(a => MapDto(a, terminalNames)).ToList();
        }, cancellationToken);
    }

    public Task<CashAccountDto> CreateAsync(
        CreateCashAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (request.Type == CashAccountType.Till)
        {
            throw new InvalidOperationException(
                "Till accounts are created via EnsureTillAccountsAsync for a terminal.");
        }

        string name = RequireName(request.Name);
        string accountCode = SanitizeAccountCode(request.AccountCode);
        string? paymentKey = SanitizePaymentMethodKey(request.PaymentMethodKey);
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            bool conflict = await context.CashAccounts.AnyAsync(
                a => a.TenantId == tenantId && a.AccountCode == accountCode,
                ct);
            if (conflict)
            {
                throw new InvalidOperationException(
                    $"Account code '{accountCode}' is already registered for this store.");
            }

            CashAccount entity = new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Type = request.Type,
                AccountCode = accountCode,
                TerminalId = null,
                IsActive = true,
                IsSystem = false,
                SortOrder = request.SortOrder,
                PaymentMethodKey = paymentKey
            };

            await context.CashAccounts.AddAsync(entity, ct);
            await context.SaveChangesAsync(ct);
            return MapDto(entity, new Dictionary<Guid, string>());
        }, cancellationToken);
    }

    public Task<CashAccountDto> UpdateAsync(
        Guid accountId,
        UpdateCashAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account id is required.", nameof(accountId));
        }

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashAccount entity = await context.CashAccounts.FirstOrDefaultAsync(
                a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
                ct)
                ?? throw new KeyNotFoundException("Cash account was not found.");

            entity.Name = RequireName(request.Name);
            entity.IsActive = request.IsActive;
            entity.SortOrder = request.SortOrder;
            entity.PaymentMethodKey = SanitizePaymentMethodKey(request.PaymentMethodKey);

            await context.SaveChangesAsync(ct);

            Dictionary<Guid, string> terminalNames = entity.TerminalId is Guid tid
                ? await LoadTerminalNamesAsync(context, [tid], ct)
                : [];

            return MapDto(entity, terminalNames);
        }, cancellationToken);
    }

    public Task<CashAccountDto> EnsureTillAccountsAsync(
        Guid terminalId,
        string terminalName,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException("Terminal id is required.", nameof(terminalId));
        }

        Guid tenantId = _tenantService.TenantId;
        string name = string.IsNullOrWhiteSpace(terminalName)
            ? $"Till - {terminalId:N}"
            : $"Till - {terminalName.Trim()}";
        if (name.Length > 100)
        {
            name = name[..100];
        }

        string accountCode = CashAccountSeeder.BuildTillAccountCode(terminalId);

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashAccount? existing = await context.CashAccounts.FirstOrDefaultAsync(
                a => a.TenantId == tenantId
                     && (a.TerminalId == terminalId || a.AccountCode == accountCode),
                ct);

            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                }

                if (!string.Equals(existing.Name, name, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(terminalName))
                {
                    existing.Name = name;
                }

                await context.SaveChangesAsync(ct);
                Dictionary<Guid, string> names = await LoadTerminalNamesAsync(context, [terminalId], ct);
                return MapDto(existing, names);
            }

            Terminal terminal = await context.Terminals.FirstOrDefaultAsync(
                t => t.Id == terminalId && t.TenantId == tenantId,
                ct)
                ?? throw new KeyNotFoundException("Terminal was not found for till account creation.");

            int maxSort = await context.CashAccounts
                .Where(a => a.TenantId == tenantId && a.Type == CashAccountType.Till)
                .Select(a => (int?)a.SortOrder)
                .MaxAsync(ct) ?? 90;

            CashAccount created = new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = string.IsNullOrWhiteSpace(terminalName)
                    ? $"Till - {terminal.TerminalName}"
                    : name,
                Type = CashAccountType.Till,
                AccountCode = accountCode,
                TerminalId = terminalId,
                IsActive = true,
                IsSystem = true,
                SortOrder = maxSort + 10,
                PaymentMethodKey = "CASH"
            };

            await context.CashAccounts.AddAsync(created, ct);
            await context.SaveChangesAsync(ct);

            Dictionary<Guid, string> terminalNames =
                await LoadTerminalNamesAsync(context, [terminalId], ct);
            return MapDto(created, terminalNames);
        }, cancellationToken);
    }

    public Task<long> GetAccountBalancePaisaAsync(
        Guid accountId,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashAccount account = await LoadAccountAsync(context, accountId, ct);
            if (asOf is null)
            {
                return account.BalancePaisa;
            }

            return await SumGlBalanceAsync(context, account.AccountCode, asOf, ct);
        }, cancellationToken);
    }

    public Task<long> GetAccountBalancePaisaByCodeAsync(
        string accountCode,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        string code = NormalizeFundingAccountCode(accountCode);
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
            await SumGlBalanceAsync(context, code, asOf, ct), cancellationToken);
    }

    public Task<CashSpendableBalanceDto> GetSpendableBalanceAsync(
        Guid accountId,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashAccount account = await LoadAccountAsync(context, accountId, ct);
            long gl = await SumGlBalanceAsync(context, account.AccountCode, asOf: null, ct);

            if (account.Type != CashAccountType.Till)
            {
                return new CashSpendableBalanceDto
                {
                    AccountId = account.Id,
                    AccountCode = SanitizeAccountCode(account.AccountCode),
                    Type = account.Type,
                    GlBalancePaisa = gl,
                    DrawerExpectedCashPaisa = null,
                    OpenShiftId = null,
                    SpendablePaisa = CashSpendable.ForNonTill(gl)
                };
            }

            if (account.TerminalId is not Guid terminalId)
            {
                throw new InvalidOperationException(
                    $"Till account '{account.Name}' is missing a terminal link.");
            }

            CashierShift? shift = null;
            if (shiftId is Guid sid)
            {
                shift = await context.CashierShifts.AsNoTracking().FirstOrDefaultAsync(
                    s => s.Id == sid
                         && s.TenantId == tenantId
                         && s.Status == "OPEN"
                         && s.TerminalId == terminalId,
                    ct);
            }

            shift ??= await context.CashierShifts.AsNoTracking()
                .Where(s =>
                    s.TenantId == tenantId
                    && s.Status == "OPEN"
                    && s.TerminalId == terminalId)
                .OrderByDescending(s => s.OpenedAt)
                .FirstOrDefaultAsync(ct);

            long drawer = shift?.ExpectedCashPaisa ?? 0L;
            return new CashSpendableBalanceDto
            {
                AccountId = account.Id,
                AccountCode = SanitizeAccountCode(account.AccountCode),
                Type = account.Type,
                GlBalancePaisa = gl,
                DrawerExpectedCashPaisa = shift is null ? null : drawer,
                OpenShiftId = shift?.Id,
                SpendablePaisa = shift is null
                    ? 0L
                    : CashSpendable.ForTill(gl, drawer)
            };
        }, cancellationToken);
    }

    public Task<CashBalancesSummaryDto> GetBalancesSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            List<CashAccount> accounts = await context.CashAccounts
                .AsNoTracking()
                .Where(a => a.TenantId == tenantId && a.IsActive)
                .OrderBy(a => a.SortOrder)
                .ThenBy(a => a.Name)
                .ToListAsync(ct);

            Dictionary<Guid, string> terminalNames = await LoadTerminalNamesAsync(
                context,
                accounts.Where(a => a.TerminalId.HasValue).Select(a => a.TerminalId!.Value),
                ct);

            DateTime todayStart = DateTime.UtcNow.Date;
            DateTime todayEnd = todayStart.AddDays(1);

            // Match GL rows case-insensitively so legacy + sanitized codes both resolve.
            List<string> codes = accounts
                .Select(a => a.AccountCode.ToUpperInvariant())
                .Distinct()
                .ToList();

            var glRows = await context.GeneralLedgerEntries
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .Select(e => new
                {
                    Code = e.AccountCode.ToUpper(),
                    e.DebitPaisa,
                    e.CreditPaisa,
                    e.CreatedAt,
                    e.TransactionType,
                    e.ShiftId
                })
                .Where(e => codes.Contains(e.Code))
                .ToListAsync(ct);

            Dictionary<string, (long Balance, long TodayIn, long TodayOut)> glByCode =
                codes.ToDictionary(
                    c => c,
                    _ => (0L, 0L, 0L),
                    StringComparer.Ordinal);

            foreach (var row in glRows)
            {
                (long balance, long todayIn, long todayOut) = glByCode[row.Code];
                balance += row.DebitPaisa - row.CreditPaisa;
                DateTime createdUtc = row.CreatedAt.UtcDateTime;
                if (createdUtc >= todayStart && createdUtc < todayEnd)
                {
                    todayIn += row.DebitPaisa;
                    todayOut += row.CreditPaisa;
                }

                glByCode[row.Code] = (balance, todayIn, todayOut);
            }

            Dictionary<(string Code, Guid ShiftId), (long Sales, long Payouts)> trailByCodeShift = [];
            foreach (var row in glRows)
            {
                if (row.ShiftId is not Guid shiftId)
                {
                    continue;
                }

                (string Code, Guid ShiftId) key = (row.Code, shiftId);
                trailByCodeShift.TryGetValue(key, out (long Sales, long Payouts) agg);
                if (IsPosSaleType(row.TransactionType))
                {
                    agg.Sales += row.DebitPaisa - row.CreditPaisa;
                }
                else if (IsTillPayoutType(row.TransactionType))
                {
                    agg.Payouts += row.CreditPaisa;
                }

                trailByCodeShift[key] = agg;
            }

            List<Guid> terminalIds = accounts
                .Where(a => a.Type == CashAccountType.Till && a.TerminalId.HasValue)
                .Select(a => a.TerminalId!.Value)
                .Distinct()
                .ToList();

            Dictionary<Guid, CashierShift> openShiftsByTerminal = [];
            Dictionary<Guid, long?> lastClosedBlindByTerminal = [];
            if (terminalIds.Count > 0)
            {
                List<CashierShift> openShifts = await context.CashierShifts
                    .AsNoTracking()
                    .Where(s =>
                        s.TenantId == tenantId
                        && s.Status == "OPEN"
                        && terminalIds.Contains(s.TerminalId))
                    .ToListAsync(ct);

                foreach (CashierShift shift in openShifts)
                {
                    openShiftsByTerminal[shift.TerminalId] = shift;
                }

                List<CashierShift> closedWithBlind = await context.CashierShifts
                    .AsNoTracking()
                    .Where(s =>
                        s.TenantId == tenantId
                        && s.Status == "CLOSED"
                        && terminalIds.Contains(s.TerminalId)
                        && s.ActualBlindCashPaisa != null)
                    .OrderByDescending(s => s.ClosedAt ?? s.OpenedAt)
                    .ToListAsync(ct);

                foreach (CashierShift shift in closedWithBlind)
                {
                    if (!lastClosedBlindByTerminal.ContainsKey(shift.TerminalId))
                    {
                        lastClosedBlindByTerminal[shift.TerminalId] = shift.ActualBlindCashPaisa;
                    }
                }
            }

            List<Guid> openShiftIds = openShiftsByTerminal.Values.Select(s => s.Id).Distinct().ToList();
            Dictionary<Guid, (long In, long Out)> unrecByShift = [];
            if (openShiftIds.Count > 0)
            {
                var movementRows = await context.ShiftCashMovements.AsNoTracking()
                    .Where(m =>
                        m.TenantId == tenantId
                        && openShiftIds.Contains(m.ShiftId)
                        && m.Status == ShiftCashMovementStatuses.Unreconciled)
                    .Select(m => new { m.ShiftId, m.Direction, m.AmountPaisa, m.Reason })
                    .ToListAsync(ct);

                foreach (var row in movementRows)
                {
                    unrecByShift.TryGetValue(row.ShiftId, out (long In, long Out) agg);
                    if (string.Equals(row.Direction, ShiftCashMovementDirections.In, StringComparison.OrdinalIgnoreCase))
                    {
                        agg.In += row.AmountPaisa;
                    }
                    else if (string.Equals(row.Reason, "Cash Shortage", StringComparison.OrdinalIgnoreCase))
                    {
                        // shortage tracked separately via Out bucket for visibility on card
                        agg.Out += row.AmountPaisa;
                    }
                    else
                    {
                        agg.Out += row.AmountPaisa;
                    }

                    unrecByShift[row.ShiftId] = agg;
                }
            }

            List<CashAccountCardDto> cards = [];
            long inTills = 0, inBank = 0, inPetty = 0, inMobile = 0, inOwner = 0, other = 0;
            long trailOpening = 0, trailSales = 0, trailManual = 0, trailPayouts = 0, trailNet = 0;

            foreach (CashAccount account in accounts)
            {
                string codeKey = account.AccountCode.ToUpperInvariant();
                (long balance, long todayIn, long todayOut) = glByCode[codeKey];
                long? drawer = null;
                long? physicalBlind = null;
                Guid? openShiftId = null;
                long unrecIn = 0;
                long unrecOut = 0;
                CashMoneyTrailDto? trail = null;
                if (account.Type == CashAccountType.Till && account.TerminalId is Guid tid)
                {
                    if (openShiftsByTerminal.TryGetValue(tid, out CashierShift? shift))
                    {
                        drawer = shift.ExpectedCashPaisa;
                        openShiftId = shift.Id;
                        physicalBlind = shift.ActualBlindCashPaisa;
                        if (unrecByShift.TryGetValue(shift.Id, out (long In, long Out) u))
                        {
                            unrecIn = u.In;
                            unrecOut = u.Out;
                        }

                        trailByCodeShift.TryGetValue((codeKey, shift.Id), out (long Sales, long Payouts) shiftTrail);
                        trail = BuildLiveTillTrail(
                            shift.OpeningCashPaisa,
                            shift.ExpectedCashPaisa,
                            shiftTrail.Sales,
                            shiftTrail.Payouts);
                        trailOpening += trail.OpeningFloatPaisa;
                        trailSales += trail.PosSalesPaisa;
                        trailManual += trail.ManualInjectionsPaisa;
                        trailPayouts += trail.PayoutsPaisa;
                        trailNet += trail.NetAvailablePaisa;
                    }
                    else if (lastClosedBlindByTerminal.TryGetValue(tid, out long? closedBlind))
                    {
                        physicalBlind = closedBlind;
                    }
                }

                long spendable = account.Type == CashAccountType.Till && drawer is long d
                    ? CashSpendable.ForTill(balance, d)
                    : CashSpendable.ForNonTill(balance);

                cards.Add(new CashAccountCardDto
                {
                    Account = MapDto(account, terminalNames),
                    DerivedGlBalancePaisa = balance,
                    TodayInPaisa = todayIn,
                    TodayOutPaisa = todayOut,
                    DrawerExpectedCashPaisa = drawer,
                    PhysicalBlindCashPaisa = physicalBlind,
                    OpenShiftId = openShiftId,
                    SpendablePaisa = spendable,
                    UnreconciledCashInPaisa = unrecIn,
                    UnreconciledCashOutPaisa = unrecOut,
                    Trail = trail
                });

                switch (account.Type)
                {
                    case CashAccountType.Till:
                        // Prefer live drawer for till KPI when open; else GL.
                        inTills += drawer ?? balance;
                        break;
                    case CashAccountType.Bank:
                        inBank += balance;
                        break;
                    case CashAccountType.Petty:
                        inPetty += balance;
                        break;
                    case CashAccountType.Mobile:
                        inMobile += balance;
                        break;
                    case CashAccountType.Owner:
                        inOwner += balance;
                        break;
                    default:
                        other += balance;
                        break;
                }
            }

            return new CashBalancesSummaryDto
            {
                Accounts = cards,
                TotalLiquidityPaisa = inTills + inBank + inPetty + inMobile + inOwner + other,
                InTillsPaisa = inTills,
                InBankPaisa = inBank,
                InPettyPaisa = inPetty,
                InMobilePaisa = inMobile,
                InOwnerPaisa = inOwner,
                TrailOpeningFloatPaisa = trailOpening,
                TrailPosSalesPaisa = trailSales,
                TrailManualInjectionsPaisa = trailManual,
                TrailPayoutsPaisa = trailPayouts,
                TrailNetAvailablePaisa = trailNet
            };
        }, cancellationToken);
    }

    public Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string paymentMethod,
        CancellationToken cancellationToken = default) =>
        ResolvePaymentAccountAsync(cashAccountId, accountCode: null, paymentMethod, cancellationToken);

    public Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string? accountCode,
        string paymentMethod,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            if (!string.IsNullOrWhiteSpace(accountCode))
            {
                string code = NormalizeFundingAccountCode(accountCode);
                CashAccount account = await context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                    a => a.TenantId == _tenantService.TenantId
                         && a.IsActive
                         && a.AccountCode.ToUpper() == code,
                    ct)
                    ?? throw new KeyNotFoundException(
                        $"Cash account with code '{code}' was not found or is inactive.");

                return ToPaymentResolution(account);
            }

            if (cashAccountId is Guid accountId)
            {
                if (accountId == Guid.Empty)
                {
                    throw new ArgumentException("Cash account id is invalid.", nameof(cashAccountId));
                }

                CashAccount account = await context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                    a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
                    ct)
                    ?? throw new KeyNotFoundException("Cash account was not found.");

                if (!account.IsActive)
                {
                    throw new InvalidOperationException(
                        $"Cash account '{account.Name}' is inactive.");
                }

                return ToPaymentResolution(account);
            }

            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                throw new ArgumentException("Payment method is required when cash account is not specified.");
            }

            string method = paymentMethod.Trim().ToUpperInvariant();
            string fallback = SanitizeAccountCode(LedgerAccounts.PaymentMethodToAccount(method));

            // Prefer a seeded/custom account with matching PaymentMethodKey when unique-ish.
            CashAccount? mapped = await context.CashAccounts
                .AsNoTracking()
                .Where(a =>
                    a.TenantId == _tenantService.TenantId
                    && a.IsActive
                    && a.PaymentMethodKey == method
                    && a.Type != CashAccountType.Till)
                .OrderBy(a => a.SortOrder)
                .FirstOrDefaultAsync(ct);

            if (mapped is not null)
            {
                return ToPaymentResolution(mapped);
            }

            return new CashPaymentResolution
            {
                AccountCode = fallback,
                CashAccountId = null,
                TerminalId = null,
                Type = null,
                AffectsTillDrawer = LedgerAccounts.IsCashAccount(fallback)
            };
        }, cancellationToken);
    }

    public Task<CashAccountLedgerDto> GetLedgerByAccountCodeAsync(
        string accountCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (to < from)
        {
            throw new ArgumentException("Query 'to' must be on or after 'from'.");
        }

        string code = NormalizeFundingAccountCode(accountCode);
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashAccount? account = await context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                a => a.TenantId == tenantId && a.AccountCode.ToUpper() == code,
                ct);

            IQueryable<GeneralLedgerEntry> baseQuery = context.GeneralLedgerEntries
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.AccountCode.ToUpper() == code);

            long? openDebits = await baseQuery
                .Where(e => e.CreatedAt < from)
                .SumAsync(e => (long?)e.DebitPaisa, ct);
            long? openCredits = await baseQuery
                .Where(e => e.CreatedAt < from)
                .SumAsync(e => (long?)e.CreditPaisa, ct);
            long opening = (openDebits ?? 0L) - (openCredits ?? 0L);

            List<GeneralLedgerEntry> rows = await baseQuery
                .Where(e => e.CreatedAt >= from && e.CreatedAt <= to)
                .OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.Id)
                .ToListAsync(ct);

            HashSet<Guid> shiftIds = rows
                .Where(r => r.ShiftId is Guid)
                .Select(r => r.ShiftId!.Value)
                .ToHashSet();

            Dictionary<Guid, CashierShift> shiftsById = shiftIds.Count == 0
                ? []
                : await context.CashierShifts.AsNoTracking()
                    .Where(s => shiftIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, ct);

            HashSet<Guid> cashierIds = shiftsById.Values.Select(s => s.CashierId).ToHashSet();
            Dictionary<Guid, string> cashierNames = cashierIds.Count == 0
                ? []
                : await context.Users.AsNoTracking()
                    .Where(u => cashierIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Username, ct);

            HashSet<Guid> terminalIds = shiftsById.Values.Select(s => s.TerminalId).ToHashSet();
            if (account?.TerminalId is Guid accountTerminalId)
            {
                terminalIds.Add(accountTerminalId);
            }

            Dictionary<Guid, string> terminalNames =
                await LoadTerminalNamesAsync(context, terminalIds, ct);

            long running = opening;
            List<CashAccountLedgerEntryDto> entries = new(rows.Count);
            foreach (GeneralLedgerEntry row in rows)
            {
                running += row.DebitPaisa - row.CreditPaisa;
                Guid? terminalId = null;
                string? terminalName = null;
                string? userName = null;
                string? shiftLabel = null;
                if (row.ShiftId is Guid sid && shiftsById.TryGetValue(sid, out CashierShift? shift))
                {
                    terminalId = shift.TerminalId;
                    terminalNames.TryGetValue(shift.TerminalId, out terminalName);
                    cashierNames.TryGetValue(shift.CashierId, out userName);
                    shiftLabel = string.IsNullOrWhiteSpace(terminalName)
                        ? sid.ToString("N")[..8]
                        : terminalName;
                }
                else if (account?.TerminalId is Guid atid)
                {
                    terminalId = atid;
                    terminalNames.TryGetValue(atid, out terminalName);
                }

                entries.Add(new CashAccountLedgerEntryDto
                {
                    Id = row.Id,
                    CreatedAt = row.CreatedAt,
                    ShiftId = row.ShiftId,
                    TerminalId = terminalId,
                    TerminalName = terminalName,
                    TransactionType = row.TransactionType,
                    ReferenceNo = row.ReferenceNo,
                    ReferenceDetails = row.ReferenceDetails,
                    DebitPaisa = row.DebitPaisa,
                    CreditPaisa = row.CreditPaisa,
                    RunningBalancePaisa = running,
                    Source = MapLedgerSource(row),
                    UserName = userName,
                    ShiftLabel = shiftLabel,
                    Status = MapLedgerStatus(row)
                });
            }

            return new CashAccountLedgerDto
            {
                AccountCode = code,
                AccountName = account?.Name,
                CashAccountId = account?.Id,
                From = from,
                To = to,
                OpeningBalancePaisa = opening,
                ClosingBalancePaisa = running,
                Entries = entries
            };
        }, cancellationToken);
    }

    public async Task<CashPaymentResolution> ResolveTillAccountForTerminalAsync(
        Guid terminalId,
        string? terminalName = null,
        CancellationToken cancellationToken = default)
    {
        CashAccountDto till = await EnsureTillAccountsAsync(
            terminalId,
            terminalName ?? string.Empty,
            cancellationToken);

        return new CashPaymentResolution
        {
            AccountCode = SanitizeAccountCode(till.AccountCode),
            CashAccountId = till.Id,
            TerminalId = till.TerminalId,
            Type = CashAccountType.Till,
            AffectsTillDrawer = true
        };
    }

    private static bool IsGracefulFloat(GeneralLedgerEntry row) =>
        string.Equals(row.TransactionType, "CASH_IN", StringComparison.OrdinalIgnoreCase)
        && row.ReferenceNo.EndsWith("-FLOAT", StringComparison.OrdinalIgnoreCase);

    private static string MapLedgerStatus(GeneralLedgerEntry row) =>
        IsGracefulFloat(row) ? "Graceful" : "Posted";

    private static string MapLedgerSource(GeneralLedgerEntry row)
    {
        if (IsGracefulFloat(row))
        {
            return "Opening Float";
        }

        string type = row.TransactionType.ToUpperInvariant();
        if (type == "SALE")
        {
            return "POS Sale";
        }

        if (type == "CASH_IN")
        {
            return row.ReferenceDetails.Contains("overage", StringComparison.OrdinalIgnoreCase)
                ? "Overage"
                : "Manual Injection";
        }

        if (type is "SUPPLIER_PAYMENT")
        {
            return "Supplier Payment";
        }

        if (type is "EXPENSE" or "RECURRING_EXPENSE" or "CASH_SHORTAGE" or "CASH_OUT")
        {
            return "Expense";
        }

        return string.IsNullOrWhiteSpace(row.TransactionType)
            ? "Manual Injection"
            : row.TransactionType.Replace("_", " ", StringComparison.Ordinal);
    }

    private static bool IsPosSaleType(string transactionType) =>
        string.Equals(transactionType, "SALE", StringComparison.OrdinalIgnoreCase);

    private static bool IsTillPayoutType(string transactionType) =>
        string.Equals(transactionType, "SUPPLIER_PAYMENT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transactionType, "EXPENSE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transactionType, "RECURRING_EXPENSE", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transactionType, "CASH_OUT", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transactionType, "CASH_TRANSFER", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transactionType, "LOAN_REPAYMENT", StringComparison.OrdinalIgnoreCase);

    private static CashMoneyTrailDto BuildLiveTillTrail(
        long openingPaisa,
        long expectedPaisa,
        long posSalesPaisa,
        long payoutsPaisa)
    {
        long opening = Math.Max(0L, openingPaisa);
        long expected = Math.Max(0L, expectedPaisa);
        long sales = Math.Max(0L, posSalesPaisa);
        long payouts = Math.Max(0L, payoutsPaisa);
        long manual = expected - opening - sales + payouts;
        if (manual < 0)
        {
            manual = 0;
        }

        return new CashMoneyTrailDto
        {
            OpeningFloatPaisa = opening,
            PosSalesPaisa = sales,
            ManualInjectionsPaisa = manual,
            PayoutsPaisa = payouts,
            NetAvailablePaisa = expected
        };
    }

    private async Task<long> SumGlBalanceAsync(
        WebPosDbContext context,
        string accountCode,
        DateTime? asOf,
        CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.TenantId;
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.AccountCode.ToUpper() == code);

        if (asOf is DateTime asOfUtc)
        {
            DateTime cutoff = DateTime.SpecifyKind(asOfUtc, DateTimeKind.Utc);
            query = query.Where(e => e.CreatedAt <= cutoff);
        }

        long? debits = await query.SumAsync(e => (long?)e.DebitPaisa, cancellationToken);
        long? credits = await query.SumAsync(e => (long?)e.CreditPaisa, cancellationToken);
        return (debits ?? 0L) - (credits ?? 0L);
    }

    private async Task<CashAccount> LoadAccountAsync(
        WebPosDbContext context,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account id is required.", nameof(accountId));
        }

        return await context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Cash account was not found.");
    }

    private async Task<Dictionary<Guid, string>> LoadTerminalNamesAsync(
        WebPosDbContext context,
        IEnumerable<Guid> terminalIds,
        CancellationToken cancellationToken)
    {
        List<Guid> ids = terminalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await context.Terminals
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.TerminalName, cancellationToken);
    }

    private static CashAccountDto MapDto(
        CashAccount account,
        IReadOnlyDictionary<Guid, string> terminalNames) =>
        new()
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type,
            AccountCode = account.AccountCode,
            TerminalId = account.TerminalId,
            TerminalName = account.TerminalId is Guid tid
                && terminalNames.TryGetValue(tid, out string? name)
                ? name
                : null,
            IsActive = account.IsActive,
            IsSystem = account.IsSystem,
            SortOrder = account.SortOrder,
            PaymentMethodKey = account.PaymentMethodKey
        };

    internal static string SanitizeAccountCode(string? accountCode)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new ArgumentException("Account code is required.");
        }

        string sanitized = accountCode.Trim().ToUpperInvariant();
        if (sanitized.Length > 50)
        {
            throw new ArgumentException("Account code must be at most 50 characters.");
        }

        return sanitized;
    }

    internal static string NormalizeFundingAccountCode(string? accountCode)
    {
        string code = SanitizeAccountCode(accountCode);
        return code switch
        {
            "OWNER_SAFE" => "OWNER_CASH",
            "CASH:MAIN" => "BANK",
            "MAIN_BANK" => "BANK",
            _ => code
        };
    }

    private static CashPaymentResolution ToPaymentResolution(CashAccount account)
    {
        string code = SanitizeAccountCode(account.AccountCode);
        bool till = account.Type == CashAccountType.Till
            || code.StartsWith("CASH:TILL:", StringComparison.OrdinalIgnoreCase);
        return new CashPaymentResolution
        {
            AccountCode = code,
            CashAccountId = account.Id,
            TerminalId = account.TerminalId,
            Type = account.Type,
            AffectsTillDrawer = till
        };
    }

    private static string RequireName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Account name is required.");
        }

        string trimmed = name.Trim();
        return trimmed.Length > 100 ? trimmed[..100] : trimmed;
    }

    private static string? SanitizePaymentMethodKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string sanitized = key.Trim().ToUpperInvariant();
        return sanitized.Length > 30 ? sanitized[..30] : sanitized;
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for cash account operations.");
        }
    }
}
