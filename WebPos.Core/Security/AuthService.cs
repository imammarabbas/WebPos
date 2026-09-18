using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.Core.Security;

public class AuthService
{
    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;

    public AuthService(IDbContextFactory<WebPosDbContext> dbFactory)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    }

    public Task<User?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult<User?>(null);
        }

        string normalized = username.Trim();
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            List<User> candidates = await context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.IsActive && u.Username.ToLower() == normalized.ToLower())
                .ToListAsync(ct);

            return candidates.FirstOrDefault(u =>
                CryptoHelper.VerifyPassword(password, u.PasswordHash));
        },
        cancellationToken);
    }
}
