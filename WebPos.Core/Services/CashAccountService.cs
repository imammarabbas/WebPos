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
    private readonly WebPosDbContext _context;
    private readonly ITenantService _tenantService;

    public CashAccountService(WebPosDbContext context, ITenantService tenantService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public async Task<IReadOnlyList<CashAccountDto>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        IQueryable<CashAccount> query = _context.CashAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(a => a.IsActive);
        }

        List<CashAccount> accounts = await query
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> terminalNames = await LoadTerminalNamesAsync(
            accounts.Where(a => a.TerminalId.HasValue).Select(a => a.TerminalId!.Value),
            cancellationToken);

        return accounts.Select(a => MapDto(a, terminalNames)).ToList();
    }

    public async Task<CashAccountDto> CreateAsync(
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

        bool conflict = await _context.CashAccounts.AnyAsync(
            a => a.TenantId == tenantId && a.AccountCode == accountCode,
            cancellationToken);
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

        await _context.CashAccounts.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return MapDto(entity, new Dictionary<Guid, string>());
    }

    public async Task<CashAccountDto> UpdateAsync(
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

        CashAccount entity = await _context.CashAccounts.FirstOrDefaultAsync(
            a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Cash account was not found.");

        entity.Name = RequireName(request.Name);
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.PaymentMethodKey = SanitizePaymentMethodKey(request.PaymentMethodKey);

        await _context.SaveChangesAsync(cancellationToken);

        Dictionary<Guid, string> terminalNames = entity.TerminalId is Guid tid
            ? await LoadTerminalNamesAsync([tid], cancellationToken)
            : [];

        return MapDto(entity, terminalNames);
    }

    public async Task<CashAccountDto> EnsureTillAccountsAsync(
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

        CashAccount? existing = await _context.CashAccounts.FirstOrDefaultAsync(
            a => a.TenantId == tenantId
                 && (a.TerminalId == terminalId || a.AccountCode == accountCode),
            cancellationToken);

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

            await _context.SaveChangesAsync(cancellationToken);
            Dictionary<Guid, string> names = await LoadTerminalNamesAsync([terminalId], cancellationToken);
            return MapDto(existing, names);
        }

        Terminal terminal = await _context.Terminals.FirstOrDefaultAsync(
            t => t.Id == terminalId && t.TenantId == tenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Terminal was not found for till account creation.");

        int maxSort = await _context.CashAccounts
            .Where(a => a.TenantId == tenantId && a.Type == CashAccountType.Till)
            .Select(a => (int?)a.SortOrder)
            .MaxAsync(cancellationToken) ?? 90;

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

        await _context.CashAccounts.AddAsync(created, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        Dictionary<Guid, string> terminalNames =
            await LoadTerminalNamesAsync([terminalId], cancellationToken);
        return MapDto(created, terminalNames);
    }

    public async Task<long> GetAccountBalancePaisaAsync(
        Guid accountId,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        CashAccount account = await LoadAccountAsync(accountId, cancellationToken);
        return await SumGlBalanceAsync(account.AccountCode, asOf, cancellationToken);
    }

    public async Task<CashBalancesSummaryDto> GetBalancesSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        List<CashAccount> accounts = await _context.CashAccounts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> terminalNames = await LoadTerminalNamesAsync(
            accounts.Where(a => a.TerminalId.HasValue).Select(a => a.TerminalId!.Value),
            cancellationToken);

        DateTime todayStart = DateTime.UtcNow.Date;
        DateTime todayEnd = todayStart.AddDays(1);

        // Match GL rows case-insensitively so legacy + sanitized codes both resolve.
        List<string> codes = accounts
            .Select(a => a.AccountCode.ToUpperInvariant())
            .Distinct()
            .ToList();

        var glRows = await _context.GeneralLedgerEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .Select(e => new
            {
                Code = e.AccountCode.ToUpper(),
                e.DebitPaisa,
                e.CreditPaisa,
                e.CreatedAt
            })
            .Where(e => codes.Contains(e.Code))
            .ToListAsync(cancellationToken);

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

        List<Guid> terminalIds = accounts
            .Where(a => a.Type == CashAccountType.Till && a.TerminalId.HasValue)
            .Select(a => a.TerminalId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, CashierShift> openShiftsByTerminal = [];
        if (terminalIds.Count > 0)
        {
            List<CashierShift> openShifts = await _context.CashierShifts
                .AsNoTracking()
                .Where(s =>
                    s.TenantId == tenantId
                    && s.Status == "OPEN"
                    && terminalIds.Contains(s.TerminalId))
                .ToListAsync(cancellationToken);

            foreach (CashierShift shift in openShifts)
            {
                openShiftsByTerminal[shift.TerminalId] = shift;
            }
        }

        List<CashAccountCardDto> cards = [];
        long inTills = 0, inBank = 0, inPetty = 0, inMobile = 0, inOwner = 0, other = 0;

        foreach (CashAccount account in accounts)
        {
            string codeKey = account.AccountCode.ToUpperInvariant();
            (long balance, long todayIn, long todayOut) = glByCode[codeKey];
            long? drawer = null;
            Guid? openShiftId = null;
            if (account.Type == CashAccountType.Till
                && account.TerminalId is Guid tid
                && openShiftsByTerminal.TryGetValue(tid, out CashierShift? shift))
            {
                drawer = shift.ExpectedCashPaisa;
                openShiftId = shift.Id;
            }

            cards.Add(new CashAccountCardDto
            {
                Account = MapDto(account, terminalNames),
                DerivedGlBalancePaisa = balance,
                TodayInPaisa = todayIn,
                TodayOutPaisa = todayOut,
                DrawerExpectedCashPaisa = drawer,
                OpenShiftId = openShiftId
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
            InOwnerPaisa = inOwner
        };
    }

    public async Task<CashPaymentResolution> ResolvePaymentAccountAsync(
        Guid? cashAccountId,
        string paymentMethod,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (cashAccountId is Guid accountId)
        {
            if (accountId == Guid.Empty)
            {
                throw new ArgumentException("Cash account id is invalid.", nameof(cashAccountId));
            }

            CashAccount account = await _context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
                a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
                cancellationToken)
                ?? throw new KeyNotFoundException("Cash account was not found.");

            if (!account.IsActive)
            {
                throw new InvalidOperationException(
                    $"Cash account '{account.Name}' is inactive.");
            }

            string code = SanitizeAccountCode(account.AccountCode);
            return new CashPaymentResolution
            {
                AccountCode = code,
                CashAccountId = account.Id,
                TerminalId = account.TerminalId,
                Type = account.Type,
                AffectsTillDrawer = account.Type == CashAccountType.Till
                    || LedgerAccounts.IsCashAccount(code)
            };
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new ArgumentException("Payment method is required when cash account is not specified.");
        }

        string method = paymentMethod.Trim().ToUpperInvariant();
        string fallback = SanitizeAccountCode(LedgerAccounts.PaymentMethodToAccount(method));

        // Prefer a seeded/custom account with matching PaymentMethodKey when unique-ish.
        CashAccount? mapped = await _context.CashAccounts
            .AsNoTracking()
            .Where(a =>
                a.TenantId == _tenantService.TenantId
                && a.IsActive
                && a.PaymentMethodKey == method
                && a.Type != CashAccountType.Till)
            .OrderBy(a => a.SortOrder)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapped is not null)
        {
            string code = SanitizeAccountCode(mapped.AccountCode);
            return new CashPaymentResolution
            {
                AccountCode = code,
                CashAccountId = mapped.Id,
                TerminalId = mapped.TerminalId,
                Type = mapped.Type,
                AffectsTillDrawer = false
            };
        }

        return new CashPaymentResolution
        {
            AccountCode = fallback,
            CashAccountId = null,
            TerminalId = null,
            Type = null,
            AffectsTillDrawer = LedgerAccounts.IsCashAccount(fallback)
        };
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

    private async Task<long> SumGlBalanceAsync(
        string accountCode,
        DateTime? asOf,
        CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.TenantId;
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = _context.GeneralLedgerEntries
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

    private async Task<CashAccount> LoadAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        if (accountId == Guid.Empty)
        {
            throw new ArgumentException("Account id is required.", nameof(accountId));
        }

        return await _context.CashAccounts.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == accountId && a.TenantId == _tenantService.TenantId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Cash account was not found.");
    }

    private async Task<Dictionary<Guid, string>> LoadTerminalNamesAsync(
        IEnumerable<Guid> terminalIds,
        CancellationToken cancellationToken)
    {
        List<Guid> ids = terminalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Terminals
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
