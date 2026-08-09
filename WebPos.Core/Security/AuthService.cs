using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Models;

namespace WebPos.Core.Security;

public class AuthService
{
    private readonly WebPosDbContext _context;

    public AuthService(WebPosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<User?> AuthenticateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        string normalized = username.Trim();
        List<User> candidates = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(u => u.Role)
            .Where(u => u.IsActive && u.Username.ToLower() == normalized.ToLower())
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(u =>
            CryptoHelper.VerifyPassword(password, u.PasswordHash));
    }
}
