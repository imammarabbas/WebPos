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
        // Role filters must stay inline so EF can translate to SQL (no local method calls).
        // IgnoreQueryFilters on Users and Roles — login happens before tenant resolution.
        CashierCredential? cashier = await (
            from user in _context.Users.IgnoreQueryFilters().AsNoTracking()
            join role in _context.Roles.IgnoreQueryFilters().AsNoTracking()
                on user.RoleId equals role.Id
            where user.IsActive
                && (role.RoleName == "Cashier"
                    || role.RoleName == "Manager"
                    || role.RoleName == "Owner"
                    || role.RoleName == "Admin")
                && user.PinHash == pinHash
            select new CashierCredential(
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
        // so verify only active terminal users that have not configured a PIN yet.
        List<CashierCredential> legacyCashiers = await (
            from user in _context.Users.IgnoreQueryFilters().AsNoTracking()
            join role in _context.Roles.IgnoreQueryFilters().AsNoTracking()
                on user.RoleId equals role.Id
            where user.IsActive
                && (role.RoleName == "Cashier"
                    || role.RoleName == "Manager"
                    || role.RoleName == "Owner"
                    || role.RoleName == "Admin")
                && user.PinHash == string.Empty
            select new CashierCredential(
                user.Id,
                user.Username,
                user.PinHash,
                user.PasswordHash))
            .ToListAsync(cancellationToken);

        cashier = legacyCashiers.FirstOrDefault(
            candidate => CryptoHelper.VerifyPassword(pin, candidate.PasswordHash));

        return cashier is null ? null : ToDto(cashier);
    }

    /// <summary>
    /// Verifies a PIN belonging to an active Owner, Manager, or Admin (not Cashier).
    /// </summary>
    public async Task<bool> VerifyManagerPinAsync(
        string pin,
        CancellationToken cancellationToken = default)
    {
        CashierDto? matched = await LoginAsync(pin, cancellationToken);
        if (matched is null)
        {
            return false;
        }

        string? roleName = await (
            from user in _context.Users.IgnoreQueryFilters().AsNoTracking()
            join role in _context.Roles.IgnoreQueryFilters().AsNoTracking()
                on user.RoleId equals role.Id
            where user.Id == matched.CashierId
            select role.RoleName)
            .FirstOrDefaultAsync(cancellationToken);

        return roleName is "Manager" or "Owner" or "Admin";
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
