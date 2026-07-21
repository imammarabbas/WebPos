using Common.Models;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;

namespace WebPos.Core.Security;

public sealed class LoginService
{
    private const int MaximumCredentialLength = 128;

    private readonly WebPosDbContext _context;
    private readonly IPinHasher _pinHasher;

    public LoginService(WebPosDbContext context, IPinHasher pinHasher)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _pinHasher = pinHasher ?? throw new ArgumentNullException(nameof(pinHasher));
    }

    public async Task<CashierDto?> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length > MaximumCredentialLength)
        {
            return null;
        }

        string pinHash = _pinHasher.HashPin(pin);
        CashierCredential? cashier = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.IsActive
                && user.Role.RoleName == "Cashier"
                && user.PinHash == pinHash)
            .Select(user => new CashierCredential(
                user.Id,
                user.Username,
                user.PinHash,
                user.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken);

        if (cashier is not null && _pinHasher.VerifyPin(pin, cashier.PinHash))
        {
            return ToDto(cashier);
        }

        // Migration fallback only: salted password hashes cannot support direct lookup,
        // so verify only active cashiers that have not configured a PIN yet.
        List<CashierCredential> legacyCashiers = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user =>
                user.IsActive
                && user.Role.RoleName == "Cashier"
                && user.PinHash == string.Empty)
            .Select(user => new CashierCredential(
                user.Id,
                user.Username,
                user.PinHash,
                user.PasswordHash))
            .ToListAsync(cancellationToken);

        cashier = legacyCashiers.FirstOrDefault(
            candidate => CryptoHelper.VerifyPassword(pin, candidate.PasswordHash));

        return cashier is null ? null : ToDto(cashier);
    }

    private static CashierDto ToDto(CashierCredential cashier) =>
        new()
        {
            CashierId = cashier.Id,
            Name = cashier.Name
        };

    private sealed record CashierCredential(
        Guid Id,
        string Name,
        string PinHash,
        string PasswordHash);
}
