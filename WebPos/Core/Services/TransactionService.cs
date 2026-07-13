using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly WebPosDbContext _context;

    public TransactionService(WebPosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction is not null)
        {
            return await action(cancellationToken);
        }

        // InMemory (and other non-relational providers) do not support ambient DB transactions.
        if (!_context.Database.IsRelational())
        {
            T inMemoryResult = await action(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return inMemoryResult;
        }

        await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            T result = await action(cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Guid> PostBalancedEntriesAsync(DoubleEntryPostRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Postings.Count == 0)
        {
            throw new ArgumentException("At least one ledger posting is required.", nameof(request));
        }

        long totalDebits = request.Postings.Sum(p => p.DebitPaisa);
        long totalCredits = request.Postings.Sum(p => p.CreditPaisa);

        if (totalDebits != totalCredits)
        {
            throw new InvalidOperationException(
                $"Ledger postings are not balanced. Debits={totalDebits}, Credits={totalCredits}.");
        }

        if (totalDebits <= 0)
        {
            throw new InvalidOperationException("Balanced postings must have a positive total amount.");
        }

        Guid transactionGroupId = request.TransactionGroupId ?? Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (LedgerPosting posting in request.Postings)
        {
            if (posting.DebitPaisa < 0 || posting.CreditPaisa < 0)
            {
                throw new ArgumentException("Debit and credit amounts cannot be negative.");
            }

            if (posting.DebitPaisa > 0 && posting.CreditPaisa > 0)
            {
                throw new ArgumentException("A posting line cannot have both debit and credit amounts.");
            }

            if (posting.DebitPaisa == 0 && posting.CreditPaisa == 0)
            {
                continue;
            }

            _context.GeneralLedgerEntries.Add(new GeneralLedgerEntry
            {
                Id = Guid.NewGuid(),
                TransactionGroupId = transactionGroupId,
                AccountCode = posting.AccountCode,
                DebitPaisa = posting.DebitPaisa,
                CreditPaisa = posting.CreditPaisa,
                TransactionType = request.TransactionType,
                ReferenceNo = request.ReferenceNo,
                ReferenceDetails = request.ReferenceDetails,
                ShiftId = request.ShiftId,
                PartyId = request.PartyId,
                CreatedAt = now
            });
        }

        return transactionGroupId;
    }
}
