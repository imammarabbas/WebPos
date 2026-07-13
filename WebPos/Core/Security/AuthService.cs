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

        User? user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.Username == username && u.IsActive,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (!CryptoHelper.VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }
}
