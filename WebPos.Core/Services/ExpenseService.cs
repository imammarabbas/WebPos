using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IAmbientDbContextAccessor _ambient;
    private readonly ITransactionService _transactionService;
    private readonly ICashAccountService _cashAccountService;
    private readonly ITenantService _tenantService;

    public ExpenseService(
        IDbContextFactory<WebPosDbContext> dbFactory,
        IAmbientDbContextAccessor ambient,
        ITransactionService transactionService,
        ICashAccountService cashAccountService,
        ITenantService tenantService)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _ambient = ambient ?? throw new ArgumentNullException(nameof(ambient));
        _transactionService = transactionService
            ?? throw new ArgumentNullException(nameof(transactionService));
        _cashAccountService = cashAccountService
            ?? throw new ArgumentNullException(nameof(cashAccountService));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
    }

    public Task<RecordExpenseResult> RecordShiftExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default) =>
        RecordExpenseInternalAsync(request, isRecurring: false, cancellationToken);

    public Task<RecordExpenseResult> RecordRecurringExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default) =>
        RecordExpenseInternalAsync(request, isRecurring: true, cancellationToken);

    public Task<IReadOnlyList<ExpenseListItemDto>> ListExpensesAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantResolved();
        if (to < from)
        {
            throw new ArgumentException("Query 'to' must be on or after 'from'.");
        }

        Guid tenantId = _tenantService.TenantId;
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            return (IReadOnlyList<ExpenseListItemDto>)await context.ShiftExpenses
                .AsNoTracking()
                .Where(e =>
                    e.TenantId == tenantId
                    && e.LoggedAt >= from
                    && e.LoggedAt <= to)
                .OrderByDescending(e => e.LoggedAt)
                .Select(e => new ExpenseListItemDto
                {
                    Id = e.Id,
                    ShiftId = e.ShiftId,
                    VoucherNo = e.VoucherNo,
                    Description = e.Description,
                    ExpenseCategory = e.ExpenseCategory,
                    PaymentMethod = e.PaymentMethod,
                    ReceiptReference = e.ReceiptReference,
                    AmountPaisa = e.AmountPaisa,
                    IsRecurring = e.IsRecurring,
                    LoggedByUserId = e.LoggedByUserId,
                    LoggedAt = e.LoggedAt
                })
                .ToListAsync(ct);
        }, cancellationToken);
    }

    private Task<RecordExpenseResult> RecordExpenseInternalAsync(
        RecordExpenseRequest request,
        bool isRecurring,
        CancellationToken cancellationToken) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            EnsureTenantResolved();
            ValidateRequest(request);
            WebPosDbContext context = _ambient.Required;

            Guid tenantId = _tenantService.TenantId;
            CashierShift? shift = null;

            if (request.ShiftId is Guid shiftId)
            {
                shift = await context.CashierShifts
                    .FirstOrDefaultAsync(
                        s => s.Id == shiftId && s.TenantId == tenantId,
                        ct)
                    ?? throw new KeyNotFoundException("Shift not found.");

                if (!string.Equals(shift.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Expenses tied to a till require an OPEN shift.");
                }
            }

            string voucherNo = string.IsNullOrWhiteSpace(request.VoucherNo)
                ? $"EXP-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}"
                : request.VoucherNo.Trim();

            bool voucherExists = await context.ShiftExpenses
                .AnyAsync(e => e.VoucherNo == voucherNo && e.TenantId == tenantId, ct);

            if (voucherExists)
            {
                throw new InvalidOperationException($"Voucher number '{voucherNo}' already exists.");
            }

            string category = request.ExpenseCategory.ToUpperInvariant();
            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            CashPaymentResolution payment = await _cashAccountService.ResolvePaymentAccountAsync(
                request.CashAccountId,
                paymentMethod,
                ct);
            EnsureExpensePaymentAccount(paymentMethod, payment);
            string paymentAccount = payment.AccountCode;
            string expenseAccount = LedgerAccounts.Expense(category);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (payment.AffectsTillDrawer)
            {
                if (shift is null)
                {
                    throw new InvalidOperationException(
                        "Till-funded expenses require an OPEN shift.");
                }

                if (payment.TerminalId is Guid tillTerminal
                    && shift.TerminalId != tillTerminal)
                {
                    throw new InvalidOperationException(
                        "Shift terminal does not match the selected till cash account.");
                }

                CashierShift locked = await CashierShiftLocking.LockByIdForUpdateAsync(
                    context,
                    shift.Id,
                    tenantId,
                    ct)
                    ?? throw new InvalidOperationException("Open shift was not found for the expense.");

                shift = locked;
                long gl = await SumAmbientGlAsync(context, tenantId, paymentAccount, ct);
                long spendable = CashSpendable.ForTill(gl, shift.ExpectedCashPaisa);
                CashSpendable.EnsureCanSpend(
                    paymentAccount,
                    spendable,
                    request.AmountPaisa,
                    shift.Id,
                    gl,
                    shift.ExpectedCashPaisa);
            }
            else
            {
                long gl = await SumAmbientGlAsync(context, tenantId, paymentAccount, ct);
                CashSpendable.EnsureCanSpend(
                    paymentAccount,
                    CashSpendable.ForNonTill(gl),
                    request.AmountPaisa);
            }

            ShiftExpense expense = new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ShiftId = request.ShiftId,
                VoucherNo = voucherNo,
                Description = request.Description.Trim(),
                AmountPaisa = request.AmountPaisa,
                ExpenseCategory = category,
                ReceiptReference = request.ReceiptReference?.Trim() ?? string.Empty,
                PaymentMethod = paymentMethod,
                IsRecurring = isRecurring,
                LoggedByUserId = request.LoggedByUserId,
                LoggedAt = now
            };
            context.ShiftExpenses.Add(expense);

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = isRecurring ? "RECURRING_EXPENSE" : "EXPENSE",
                    ReferenceNo = voucherNo,
                    ReferenceDetails = expense.ReceiptReference,
                    ShiftId = request.ShiftId,
                    Postings =
                    [
                        new LedgerPosting
                        {
                            AccountCode = expenseAccount,
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

            if (shift is not null && payment.AffectsTillDrawer)
            {
                shift.ExpectedCashPaisa -= request.AmountPaisa;
            }

            return new RecordExpenseResult
            {
                ExpenseId = expense.Id,
                VoucherNo = voucherNo,
                TransactionGroupId = transactionGroupId
            };
        }, cancellationToken);

    private static async Task<long> SumAmbientGlAsync(
        WebPosDbContext context,
        Guid tenantId,
        string accountCode,
        CancellationToken cancellationToken)
    {
        string code = accountCode.ToUpperInvariant();
        IQueryable<GeneralLedgerEntry> query = context.GeneralLedgerEntries
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
                "Tenant context is not resolved for expense operations.");
        }
    }

    private static void ValidateRequest(RecordExpenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Expense amount must be greater than zero Paisa.");
        }

        if (request.LoggedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Logged-by user is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpenseCategory))
        {
            throw new ArgumentException("Expense category is required.");
        }

        if (!ExpenseCategories.All.Contains(
                request.ExpenseCategory.Trim(),
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Expense category must be one of: {string.Join(", ", ExpenseCategories.All)}.");
        }

        if (ExpenseCategories.RequiresReceiptReference(request.ExpenseCategory)
            && string.IsNullOrWhiteSpace(request.ReceiptReference))
        {
            throw new ArgumentException(
                "Receipt reference is required for maintenance and equipment expenses.");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            throw new ArgumentException("Payment method is required.");
        }

        if (string.Equals(request.PaymentMethod, "CREDIT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Expenses cannot use CREDIT as the payment channel.");
        }

        if (string.Equals(request.PaymentMethod, "CASH", StringComparison.OrdinalIgnoreCase)
            && request.CashAccountId is null)
        {
            throw new ArgumentException(
                "Cash expenses require an explicit funding account (Till, Owner, Petty, etc.).");
        }
    }

    private static void EnsureExpensePaymentAccount(
        string paymentMethod,
        CashPaymentResolution payment)
    {
        if (!string.Equals(paymentMethod, "CASH", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(payment.AccountCode, LedgerAccounts.Cash, StringComparison.OrdinalIgnoreCase)
            && payment.CashAccountId is null)
        {
            throw new ArgumentException(
                "Cash expenses must use a specific funding account, not the legacy generic CASH account.");
        }
    }
}
