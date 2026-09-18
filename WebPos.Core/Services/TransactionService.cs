using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Services;

public sealed class TransactionService : ITransactionService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;

    public TransactionService(IDbContextFactory<WebPosDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct =>
        {
            await action(ct);
            return true;
        }, cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        WebPosDbContext? ambient = AmbientDbContextAccessor.GetAmbient();
        if (ambient is not null)
        {
            return await action(cancellationToken);
        }

        await using WebPosDbContext context = await _dbFactory.CreateDbContextAsync(cancellationToken);
        AmbientDbContextAccessor.SetAmbient(context);
        try
        {
            // InMemory (and other non-relational providers) do not support ambient DB transactions.
            if (!context.Database.IsRelational())
            {
                T inMemoryResult = await action(cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                return inMemoryResult;
            }

            await using IDbContextTransaction transaction =
                await context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                T result = await action(cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await TryRollbackAsync(transaction);
                throw;
            }
        }
        finally
        {
            AmbientDbContextAccessor.SetAmbient(null);
        }
    }

    public async Task<Guid> PostBalancedEntriesAsync(
        DoubleEntryPostRequest request,
        CancellationToken cancellationToken = default)
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

        WebPosDbContext context = AmbientDbContextAccessor.GetAmbient()
            ?? throw new InvalidOperationException(
                "PostBalancedEntriesAsync requires an ambient transaction context.");

        Guid transactionGroupId = request.TransactionGroupId ?? Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Dictionary<string, long> cashDeltas = new(StringComparer.OrdinalIgnoreCase);

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

            context.GeneralLedgerEntries.Add(new GeneralLedgerEntry
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

            long delta = posting.DebitPaisa - posting.CreditPaisa;
            if (delta != 0 && !string.IsNullOrWhiteSpace(posting.AccountCode))
            {
                string code = posting.AccountCode.Trim();
                cashDeltas[code] = cashDeltas.TryGetValue(code, out long existing)
                    ? existing + delta
                    : delta;
            }
        }

        await ApplyCashAccountBalanceDeltasAsync(context, cashDeltas, cancellationToken);

        return transactionGroupId;
    }

    private static async Task ApplyCashAccountBalanceDeltasAsync(
        WebPosDbContext context,
        Dictionary<string, long> deltasByCode,
        CancellationToken cancellationToken)
    {
        if (deltasByCode.Count == 0)
        {
            return;
        }

        List<string> codes = deltasByCode.Keys.Select(c => c.ToUpperInvariant()).ToList();
        List<CashAccount> accounts = await context.CashAccounts
            .Where(a => codes.Contains(a.AccountCode.ToUpper()))
            .ToListAsync(cancellationToken);

        foreach (CashAccount account in accounts)
        {
            if (!deltasByCode.TryGetValue(account.AccountCode, out long delta))
            {
                // Case-insensitive match fallback
                KeyValuePair<string, long> match = deltasByCode
                    .FirstOrDefault(kv =>
                        string.Equals(kv.Key, account.AccountCode, StringComparison.OrdinalIgnoreCase));
                if (match.Key is null)
                {
                    continue;
                }

                delta = match.Value;
            }

            if (delta == 0)
            {
                continue;
            }

            long next = account.BalancePaisa + delta;
            if (next < 0)
            {
                throw new InsufficientCashBalanceException(
                    account.AccountCode,
                    Math.Max(0L, account.BalancePaisa),
                    Math.Abs(delta));
            }

            account.BalancePaisa = next;
        }
    }

    private static async Task TryRollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
