using Microsoft.EntityFrameworkCore;
using WebPos.Core.Abstractions;
using WebPos.Core.Constants;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class ExpenseService : IExpenseService
{
    private readonly WebPosDbContext _context;
    private readonly ITransactionService _transactionService;

    public ExpenseService(WebPosDbContext context, ITransactionService transactionService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
    }

    public Task<RecordExpenseResult> RecordShiftExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default) =>
        RecordExpenseInternalAsync(request, isRecurring: false, cancellationToken);

    public Task<RecordExpenseResult> RecordRecurringExpenseAsync(
        RecordExpenseRequest request,
        CancellationToken cancellationToken = default) =>
        RecordExpenseInternalAsync(request, isRecurring: true, cancellationToken);

    private Task<RecordExpenseResult> RecordExpenseInternalAsync(
        RecordExpenseRequest request,
        bool isRecurring,
        CancellationToken cancellationToken) =>
        _transactionService.ExecuteInTransactionAsync(async ct =>
        {
            ValidateRequest(request);

            CashierShift? shift = await _context.CashierShifts
                .FirstOrDefaultAsync(s => s.Id == request.ShiftId, ct);

            if (shift is null)
            {
                throw new KeyNotFoundException("Shift not found.");
            }

            string voucherNo = string.IsNullOrWhiteSpace(request.VoucherNo)
                ? $"EXP-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Random.Shared.Next(1000, 9999)}"
                : request.VoucherNo.Trim();

            bool voucherExists = await _context.ShiftExpenses
                .AnyAsync(e => e.VoucherNo == voucherNo, ct);

            if (voucherExists)
            {
                throw new InvalidOperationException($"Voucher number '{voucherNo}' already exists.");
            }

            string category = request.ExpenseCategory.ToUpperInvariant();
            string paymentMethod = request.PaymentMethod.ToUpperInvariant();
            string paymentAccount = LedgerAccounts.PaymentMethodToAccount(paymentMethod);
            string expenseAccount = LedgerAccounts.Expense(category);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            ShiftExpense expense = new()
            {
                Id = Guid.NewGuid(),
                ShiftId = request.ShiftId,
                VoucherNo = voucherNo,
                Description = request.Description.Trim(),
                AmountPaisa = request.AmountPaisa,
                ExpenseCategory = category,
                ReceiptReference = request.ReceiptReference.Trim(),
                PaymentMethod = paymentMethod,
                IsRecurring = isRecurring,
                LoggedByUserId = request.LoggedByUserId,
                LoggedAt = now
            };
            _context.ShiftExpenses.Add(expense);

            Guid transactionGroupId = await _transactionService.PostBalancedEntriesAsync(
                new DoubleEntryPostRequest
                {
                    TransactionType = isRecurring ? "RECURRING_EXPENSE" : "EXPENSE",
                    ReferenceNo = voucherNo,
                    ReferenceDetails = request.ReceiptReference.Trim(),
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

            if (LedgerAccounts.IsCashAccount(paymentAccount))
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

    private static void ValidateRequest(RecordExpenseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AmountPaisa <= 0)
        {
            throw new ArgumentException("Expense amount must be greater than zero Paisa.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpenseCategory))
        {
            throw new ArgumentException("Expense category is required.");
        }

        if (ExpenseCategories.RequiresReceiptReference(request.ExpenseCategory)
            && string.IsNullOrWhiteSpace(request.ReceiptReference))
        {
            throw new ArgumentException("Receipt reference is required for maintenance expenses.");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            throw new ArgumentException("Payment method is required.");
        }
    }
}
