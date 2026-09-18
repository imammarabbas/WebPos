using Common.Models;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public interface IShiftService
{
    Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> CloseShiftAsync(
        CloseShiftRequest request,
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> GetCashReconciliationAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<OpenShiftDto?> GetOpenShiftForTerminalAsync(
        Guid terminalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpenShiftDto>> ListOpenShiftsAsync(
        CancellationToken cancellationToken = default);

    Task<CashVarianceReport> ForceCloseShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default);

    Task<SuggestedOpeningCashDto> GetSuggestedOpeningCashAsync(
        Guid terminalId,
        CancellationToken cancellationToken = default);
}

public sealed class ShiftService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    IAmbientDbContextAccessor ambient,
    ITransactionService transactionService,
    ITenantService tenantService,
    ICashAccountService cashAccountService) : IShiftService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly IAmbientDbContextAccessor _ambient =
        ambient ?? throw new ArgumentNullException(nameof(ambient));
    private readonly ITransactionService _transactionService =
        transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));
    private readonly ICashAccountService _cashAccountService =
        cashAccountService ?? throw new ArgumentNullException(nameof(cashAccountService));

    public Task<ShiftDto> StartShiftAsync(
        StartShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            if (request.CashierId == Guid.Empty || request.TerminalId == Guid.Empty)
            {
                throw new InvalidOperationException("Cashier and terminal are required.");
            }

            if (request.OpeningCashPaisa < 0)
            {
                throw new InvalidOperationException("Opening cash cannot be negative.");
            }

            bool operatorIsValid = await _ambient.Required.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.Id == request.CashierId
                    && user.TenantId == _tenantService.TenantId
                    && user.IsActive
                    && (user.Role.RoleName == "Cashier"
                        || user.Role.RoleName == "Manager"
                        || user.Role.RoleName == "Owner"
                        || user.Role.RoleName == "Admin"),
                    ct);

            if (!operatorIsValid)
            {
                throw new ShiftAuthorizationException("The user is not active or authorized to open a shift.");
            }

            bool terminalIsValid = await _ambient.Required.Terminals
                .AsNoTracking()
                .AnyAsync(terminal =>
                    terminal.Id == request.TerminalId
                    && terminal.TenantId == _tenantService.TenantId
                    && terminal.IsActive,
                    ct);

            if (!terminalIsValid)
            {
                throw new ShiftAuthorizationException("The terminal is not active or registered.");
            }

            CashierShift? terminalShift = await _ambient.Required.CashierShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(shift =>
                    shift.TerminalId == request.TerminalId
                    && shift.TenantId == _tenantService.TenantId
                    && shift.Status == "OPEN",
                    ct);

            if (terminalShift is not null)
            {
                if (terminalShift.CashierId == request.CashierId)
                {
                    return ToDto(terminalShift);
                }

                throw new ShiftConflictException(
                    "This terminal already has an open shift for another cashier.");
            }

            bool cashierHasOpenShift = await _ambient.Required.CashierShifts
                .AsNoTracking()
                .AnyAsync(shift =>
                    shift.CashierId == request.CashierId
                    && shift.TenantId == _tenantService.TenantId
                    && shift.Status == "OPEN",
                    ct);

            if (cashierHasOpenShift)
            {
                throw new ShiftConflictException(
                    "This cashier already has an open shift on another terminal.");
            }

            CashierShift shift = new()
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantService.TenantId,
                TerminalId = request.TerminalId,
                CashierId = request.CashierId,
                OpenedAt = DateTimeOffset.UtcNow,
                OpeningCashPaisa = request.OpeningCashPaisa,
                ExpectedCashPaisa = request.OpeningCashPaisa,
                DiscrepancyPaisa = 0,
                Status = "OPEN"
            };

            _ambient.Required.CashierShifts.Add(shift);
            await _ambient.Required.SaveChangesAsync(ct);
            return ToDto(shift);
        }, cancellationToken);
    }

    public Task<CashVarianceReport> CloseShiftAsync(
        CloseShiftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureTenantResolved();

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            if (request.ShiftId == Guid.Empty)
            {
                throw new InvalidOperationException("ShiftId is required.");
            }

            if (request.ActualCashPaisa < 0)
            {
                throw new InvalidOperationException("Actual cash cannot be negative.");
            }

            CashierShift shift = await _ambient.Required.CashierShifts
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == request.ShiftId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new ShiftAuthorizationException(
                    "The shift was not found for the current tenant.");

            if (!string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                throw new ShiftConflictException(
                    "Only an OPEN shift can be closed.");
            }

            if (request.TerminalId != Guid.Empty
                && shift.TerminalId != request.TerminalId)
            {
                throw new ShiftAuthorizationException(
                    "The shift is not authorized for this terminal.");
            }

            if (request.CashierId != Guid.Empty
                && shift.CashierId != request.CashierId)
            {
                throw new ShiftAuthorizationException(
                    "The shift is not authorized for this cashier.");
            }

            long expectedCashPaisa = shift.ExpectedCashPaisa;
            long actualCashPaisa = request.ActualCashPaisa;

            // Physical count is observation only — never overwrite ExpectedCash.
            // Ledger vs ExpectedCash gaps are mid-shift Cash In / Shortage; not a close hard-block.
            _ = request.ResolveLedgerShortagePaisa;

            long discrepancyPaisa = actualCashPaisa - expectedCashPaisa;

            shift.ActualBlindCashPaisa = actualCashPaisa;
            shift.DiscrepancyPaisa = discrepancyPaisa;
            shift.ClosedAt = DateTimeOffset.UtcNow;
            shift.Status = "CLOSED";
            // ExpectedCashPaisa intentionally unchanged.

            await _ambient.Required.SaveChangesAsync(ct);

            return await BuildVarianceReportAsync(
                _ambient.Required,
                shift,
                expectedCashPaisa,
                actualCashPaisa,
                discrepancyPaisa,
                ct);
        }, cancellationToken);
    }

    public Task<CashVarianceReport> GetCashReconciliationAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (shiftId == Guid.Empty)
        {
            throw new InvalidOperationException("ShiftId is required.");
        }

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashierShift shift = await context.CashierShifts
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == shiftId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new ShiftAuthorizationException(
                    "The shift was not found for the current tenant.");

            long expectedCashPaisa = shift.ExpectedCashPaisa;
            long actualCashPaisa = shift.ActualBlindCashPaisa ?? 0;
            long discrepancyPaisa = string.Equals(shift.Status, "CLOSED", StringComparison.OrdinalIgnoreCase)
                ? shift.DiscrepancyPaisa
                : actualCashPaisa - expectedCashPaisa;

            return await BuildVarianceReportAsync(
                context,
                shift,
                expectedCashPaisa,
                actualCashPaisa,
                discrepancyPaisa,
                ct);
        }, cancellationToken);
    }

    public Task<OpenShiftDto?> GetOpenShiftForTerminalAsync(
        Guid terminalId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (terminalId == Guid.Empty)
        {
            throw new InvalidOperationException("Terminal id is required.");
        }

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            OpenShiftDto? open = await QueryOpenShifts(context)
                .Where(shift => shift.TerminalId == terminalId)
                .FirstOrDefaultAsync(ct);
            if (open is null)
            {
                return null;
            }

            return await EnrichOpenShiftAsync(context, open, ct);
        }, cancellationToken);
    }

    public Task<IReadOnlyList<OpenShiftDto>> ListOpenShiftsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            List<OpenShiftDto> open = await QueryOpenShifts(context)
                .OrderByDescending(shift => shift.OpenedAt)
                .ToListAsync(ct);
            List<OpenShiftDto> enriched = [];
            foreach (OpenShiftDto row in open)
            {
                enriched.Add(await EnrichOpenShiftAsync(context, row, ct));
            }

            return (IReadOnlyList<OpenShiftDto>)enriched;
        }, cancellationToken);
    }

    public Task<CashVarianceReport> ForceCloseShiftAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (shiftId == Guid.Empty)
        {
            throw new InvalidOperationException("Shift id is required.");
        }

        return _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            WebPosDbContext context = _ambient.Required;
            CashierShift shift = await context.CashierShifts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    candidate =>
                        candidate.Id == shiftId
                        && candidate.TenantId == _tenantService.TenantId,
                    ct)
                ?? throw new KeyNotFoundException("Open shift was not found.");

            if (!string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
            {
                throw new ShiftConflictException("Only an OPEN shift can be force-closed.");
            }

            long expectedCashPaisa = shift.ExpectedCashPaisa;
            // No physical observation — record Actual = Expected for a balanced force-close.
            // Do not mutate ExpectedCash; do not auto-post Ledger−Expected as shortage.
            shift.ActualBlindCashPaisa = expectedCashPaisa;
            shift.DiscrepancyPaisa = 0;
            shift.ClosedAt = DateTimeOffset.UtcNow;
            shift.Status = "CLOSED";
            await context.SaveChangesAsync(ct);

            return await BuildVarianceReportAsync(
                context,
                shift,
                expectedCashPaisa,
                expectedCashPaisa,
                0,
                ct);
        }, cancellationToken);
    }

    public Task<SuggestedOpeningCashDto> GetSuggestedOpeningCashAsync(
        Guid terminalId,
        CancellationToken cancellationToken = default)
    {
        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException("Terminal id is required.");
        }

        EnsureTenantResolved();
        Guid tenantId = _tenantService.TenantId;

        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            CashPaymentResolution till = await _cashAccountService.ResolveTillAccountForTerminalAsync(
                terminalId,
                cancellationToken: ct);
            long tillGl = await SumGlAsync(context, till.AccountCode, ct);

            CashierShift? lastClosed = await context.CashierShifts
                .AsNoTracking()
                .Where(s =>
                    s.TenantId == tenantId
                    && s.TerminalId == terminalId
                    && s.Status == "CLOSED")
                .OrderByDescending(s => s.ClosedAt ?? s.OpenedAt)
                .FirstOrDefaultAsync(ct);

            return new SuggestedOpeningCashDto
            {
                TerminalId = terminalId,
                TillGlPaisa = tillGl,
                LastExpectedCashPaisa = lastClosed?.ExpectedCashPaisa,
                LastPhysicalCountPaisa = lastClosed?.ActualBlindCashPaisa,
                SuggestedOpeningPaisa = tillGl
            };
        }, cancellationToken);
    }

    private async Task<long> SumGlAsync(
        WebPosDbContext context,
        string accountCode,
        CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.TenantId;
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = context.GeneralLedgerEntries
            .Where(e => e.TenantId == tenantId && e.AccountCode.ToUpper() == code);

        long? debits = await query.SumAsync(e => (long?)e.DebitPaisa, cancellationToken);
        long? credits = await query.SumAsync(e => (long?)e.CreditPaisa, cancellationToken);
        return (debits ?? 0L) - (credits ?? 0L);
    }

    private async Task<(long In, long Out)> SumUnreconciledAsync(
        WebPosDbContext context,
        Guid shiftId,
        CancellationToken cancellationToken)
    {
        Guid tenantId = _tenantService.TenantId;
        var rows = await context.ShiftCashMovements.AsNoTracking()
            .Where(m =>
                m.TenantId == tenantId
                && m.ShiftId == shiftId
                && m.Status == ShiftCashMovementStatuses.Unreconciled)
            .Select(m => new { m.Direction, m.AmountPaisa })
            .ToListAsync(cancellationToken);

        long inn = 0;
        long outt = 0;
        foreach (var row in rows)
        {
            if (string.Equals(row.Direction, ShiftCashMovementDirections.In, StringComparison.OrdinalIgnoreCase))
            {
                inn += row.AmountPaisa;
            }
            else
            {
                outt += row.AmountPaisa;
            }
        }

        return (inn, outt);
    }

    private async Task<OpenShiftDto> EnrichOpenShiftAsync(
        WebPosDbContext context,
        OpenShiftDto open,
        CancellationToken cancellationToken)
    {
        CashPaymentResolution till = await _cashAccountService.ResolveTillAccountForTerminalAsync(
            open.TerminalId,
            cancellationToken: cancellationToken);
        long registered = await SumGlAsync(context, till.AccountCode, cancellationToken);
        (long unrecIn, long unrecOut) = await SumUnreconciledAsync(context, open.ShiftId, cancellationToken);
        long available = CashSpendable.ForTill(registered, open.ExpectedCashPaisa);

        return new OpenShiftDto
        {
            ShiftId = open.ShiftId,
            CashierId = open.CashierId,
            CashierName = open.CashierName,
            TerminalId = open.TerminalId,
            TerminalName = open.TerminalName,
            OpenedAt = open.OpenedAt,
            OpeningCashPaisa = open.OpeningCashPaisa,
            ExpectedCashPaisa = open.ExpectedCashPaisa,
            RegisteredLedgerPaisa = registered,
            AvailablePaisa = available,
            UnreconciledCashInPaisa = unrecIn,
            UnreconciledCashOutPaisa = unrecOut
        };
    }

    private IQueryable<OpenShiftDto> QueryOpenShifts(WebPosDbContext context) =>
        context.CashierShifts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(shift =>
                shift.TenantId == _tenantService.TenantId
                && shift.Status == "OPEN")
            .Select(shift => new OpenShiftDto
            {
                ShiftId = shift.Id,
                CashierId = shift.CashierId,
                CashierName = context.Users
                    .IgnoreQueryFilters()
                    .Where(user => user.Id == shift.CashierId)
                    .Select(user => user.Username)
                    .FirstOrDefault() ?? "Unknown",
                TerminalId = shift.TerminalId,
                TerminalName = context.Terminals
                    .IgnoreQueryFilters()
                    .Where(terminal => terminal.Id == shift.TerminalId)
                    .Select(terminal => terminal.TerminalName)
                    .FirstOrDefault(),
                OpenedAt = shift.OpenedAt,
                OpeningCashPaisa = shift.OpeningCashPaisa,
                ExpectedCashPaisa = shift.ExpectedCashPaisa,
                RegisteredLedgerPaisa = 0,
                AvailablePaisa = 0,
                UnreconciledCashInPaisa = 0,
                UnreconciledCashOutPaisa = 0
            });

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for shift operations.");
        }
    }

    private async Task<CashVarianceReport> BuildVarianceReportAsync(
        WebPosDbContext context,
        CashierShift shift,
        long expectedCashPaisa,
        long actualCashPaisa,
        long discrepancyPaisa,
        CancellationToken cancellationToken)
    {
        long registered = 0;
        long available = 0;
        try
        {
            CashPaymentResolution till = await _cashAccountService.ResolveTillAccountForTerminalAsync(
                shift.TerminalId,
                cancellationToken: cancellationToken);
            registered = await SumGlAsync(context, till.AccountCode, cancellationToken);
            available = CashSpendable.ForTill(registered, expectedCashPaisa);
        }
        catch
        {
            /* till may be missing in edge cases — still return variance */
        }

        (long unrecIn, long unrecOut) = await SumUnreconciledAsync(context, shift.Id, cancellationToken);
        string reconciliationStatus = ResolveReconciliationStatus(
            discrepancyPaisa,
            unrecIn + unrecOut);

        return new CashVarianceReport
        {
            ShiftId = shift.Id,
            TerminalId = shift.TerminalId,
            CashierId = shift.CashierId,
            Status = shift.Status,
            OpeningCashPaisa = shift.OpeningCashPaisa,
            ExpectedCashPaisa = expectedCashPaisa,
            ActualCashPaisa = actualCashPaisa,
            PhysicalCountPaisa = actualCashPaisa,
            DiscrepancyPaisa = discrepancyPaisa,
            IsBalanced = discrepancyPaisa == 0,
            ClosedAt = shift.ClosedAt,
            RegisteredLedgerPaisa = registered,
            AvailablePaisa = available,
            UnreconciledCashInPaisa = unrecIn,
            UnreconciledCashOutPaisa = unrecOut,
            ReconciliationStatus = reconciliationStatus
        };
    }

    private static string ResolveReconciliationStatus(long discrepancyPaisa, long unreconciledTotalPaisa)
    {
        if (discrepancyPaisa < 0)
        {
            return "SHORTAGE";
        }

        if (discrepancyPaisa > 0)
        {
            return "OVERAGE";
        }

        if (unreconciledTotalPaisa > 0)
        {
            return "UNRECONCILED";
        }

        return "MATCHED";
    }

    private static ShiftDto ToDto(CashierShift shift) =>
        new()
        {
            ShiftId = shift.Id,
            CashierId = shift.CashierId,
            TerminalId = shift.TerminalId,
            OpenedAt = shift.OpenedAt,
            Status = shift.Status
        };
}

public sealed class ShiftAuthorizationException(string message) : Exception(message);

public sealed class ShiftConflictException(string message) : Exception(message);
