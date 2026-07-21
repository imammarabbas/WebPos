using Common.Models;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
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
}

public sealed class ShiftService(
    WebPosDbContext context,
    ITransactionService transactionService,
    ITenantService tenantService) : IShiftService
{
    private readonly WebPosDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
    private readonly ITransactionService _transactionService =
        transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    private readonly ITenantService _tenantService =
        tenantService ?? throw new ArgumentNullException(nameof(tenantService));

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

            bool cashierIsValid = await _context.Users
                .AsNoTracking()
                .AnyAsync(user =>
                    user.Id == request.CashierId
                    && user.TenantId == _tenantService.TenantId
                    && user.IsActive
                    && user.Role.RoleName == "Cashier",
                    ct);

            if (!cashierIsValid)
            {
                throw new ShiftAuthorizationException("The cashier is not active or authorized.");
            }

            bool terminalIsValid = await _context.Terminals
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

            CashierShift? terminalShift = await _context.CashierShifts
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

            bool cashierHasOpenShift = await _context.CashierShifts
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

            _context.CashierShifts.Add(shift);
            await _context.SaveChangesAsync(ct);
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

            CashierShift shift = await _context.CashierShifts
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
            long discrepancyPaisa = actualCashPaisa - expectedCashPaisa;

            shift.ActualBlindCashPaisa = actualCashPaisa;
            shift.DiscrepancyPaisa = discrepancyPaisa;
            shift.ClosedAt = DateTimeOffset.UtcNow;
            shift.Status = "CLOSED";

            await _context.SaveChangesAsync(ct);

            return BuildVarianceReport(shift, expectedCashPaisa, actualCashPaisa, discrepancyPaisa);
        }, cancellationToken);
    }

    public async Task<CashVarianceReport> GetCashReconciliationAsync(
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();

        if (shiftId == Guid.Empty)
        {
            throw new InvalidOperationException("ShiftId is required.");
        }

        CashierShift shift = await _context.CashierShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == shiftId
                    && candidate.TenantId == _tenantService.TenantId,
                cancellationToken)
            ?? throw new ShiftAuthorizationException(
                "The shift was not found for the current tenant.");

        long expectedCashPaisa = shift.ExpectedCashPaisa;
        long actualCashPaisa = shift.ActualBlindCashPaisa ?? 0;
        long discrepancyPaisa = shift.Status == "CLOSED"
            ? shift.DiscrepancyPaisa
            : actualCashPaisa - expectedCashPaisa;

        return BuildVarianceReport(shift, expectedCashPaisa, actualCashPaisa, discrepancyPaisa);
    }

    private void EnsureTenantResolved()
    {
        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is not resolved for shift operations.");
        }
    }

    private static CashVarianceReport BuildVarianceReport(
        CashierShift shift,
        long expectedCashPaisa,
        long actualCashPaisa,
        long discrepancyPaisa) =>
        new()
        {
            ShiftId = shift.Id,
            TerminalId = shift.TerminalId,
            CashierId = shift.CashierId,
            Status = shift.Status,
            OpeningCashPaisa = shift.OpeningCashPaisa,
            ExpectedCashPaisa = expectedCashPaisa,
            ActualCashPaisa = actualCashPaisa,
            DiscrepancyPaisa = discrepancyPaisa,
            IsBalanced = discrepancyPaisa == 0,
            ClosedAt = shift.ClosedAt
        };

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
