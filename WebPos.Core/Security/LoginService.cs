using Common.Models;
using Microsoft.EntityFrameworkCore;
using WebPos.Core.Data;
using WebPos.Core.Services;

namespace WebPos.Core.Security;

public sealed class LoginService
{
    private const int MaximumCredentialLength = 128;

    private readonly IDbContextFactory<WebPosDbContext> _dbFactory;
    private readonly IPinHasher _pinHasher;

    public LoginService(IDbContextFactory<WebPosDbContext> dbFactory, IPinHasher pinHasher)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _pinHasher = pinHasher ?? throw new ArgumentNullException(nameof(pinHasher));
    }

    public Task<CashierDto?> LoginAsync(
        string pin,
        CancellationToken cancellationToken = default) =>
        LoginAsync(pin, expectedRole: null, cancellationToken);

    public Task<CashierDto?> LoginAsync(
        string pin,
        string? expectedRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pin) || pin.Length > MaximumCredentialLength)
        {
            return Task.FromResult<CashierDto?>(null);
        }

        string? roleFilter = NormalizeRole(expectedRole);
        string pinHash = _pinHasher.HashPin(pin);
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            // Role filters must stay inline so EF can translate to SQL (no local method calls).
            // IgnoreQueryFilters on Users and Roles — login happens before tenant resolution.
            CashierCredential? cashier = await (
                from user in context.Users.IgnoreQueryFilters().AsNoTracking()
                join role in context.Roles.IgnoreQueryFilters().AsNoTracking()
                    on user.RoleId equals role.Id
                where user.IsActive
                    && (role.RoleName == "Cashier"
                        || role.RoleName == "Manager"
                        || role.RoleName == "Owner"
                        || role.RoleName == "Admin")
                    && (roleFilter == null
                        || role.RoleName == roleFilter
                        // Tenants that renamed Admin → Manager still accept Admin on the lock screen.
                        || (roleFilter == "Admin" && role.RoleName == "Manager"))
                    && user.PinHash == pinHash
                select new CashierCredential(
                    user.Id,
                    user.Username,
                    user.PinHash,
                    user.PasswordHash,
                    role.RoleName))
                .SingleOrDefaultAsync(ct);

            if (cashier is not null && _pinHasher.VerifyPin(pin, cashier.PinHash))
            {
                return ToDto(cashier);
            }

            // Migration fallback only: salted password hashes cannot support direct lookup,
            // so verify only active terminal users that have not configured a PIN yet.
            List<CashierCredential> legacyCashiers = await (
                from user in context.Users.IgnoreQueryFilters().AsNoTracking()
                join role in context.Roles.IgnoreQueryFilters().AsNoTracking()
                    on user.RoleId equals role.Id
                where user.IsActive
                    && (role.RoleName == "Cashier"
                        || role.RoleName == "Manager"
                        || role.RoleName == "Owner"
                        || role.RoleName == "Admin")
                    && (roleFilter == null
                        || role.RoleName == roleFilter
                        || (roleFilter == "Admin" && role.RoleName == "Manager"))
                    && user.PinHash == string.Empty
                select new CashierCredential(
                    user.Id,
                    user.Username,
                    user.PinHash,
                    user.PasswordHash,
                    role.RoleName))
                .ToListAsync(ct);

            cashier = legacyCashiers.FirstOrDefault(
                candidate => CryptoHelper.VerifyPassword(pin, candidate.PasswordHash));

            return cashier is null ? null : ToDto(cashier);
        },
        cancellationToken);
    }

    private static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return role.Trim() switch
        {
            "Cashier" => "Cashier",
            "Manager" => "Manager",
            "Owner" => "Owner",
            "Admin" => "Admin",
            _ => null
        };
    }

    /// <summary>
    /// Terminal login with the same username/password as the master website.
    /// </summary>
    public Task<CashierDto?> LoginWithPasswordAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || username.Length > MaximumCredentialLength
            || password.Length > MaximumCredentialLength)
        {
            return Task.FromResult<CashierDto?>(null);
        }

        string normalized = username.Trim();
        return DbContextExecution.ExecuteAsync(_dbFactory, async (context, ct) =>
        {
            List<CashierCredential> candidates = await (
                from user in context.Users.IgnoreQueryFilters().AsNoTracking()
                join role in context.Roles.IgnoreQueryFilters().AsNoTracking()
                    on user.RoleId equals role.Id
                where user.IsActive
                    && user.Username.ToLower() == normalized.ToLower()
                    && (role.RoleName == "Cashier"
                        || role.RoleName == "Manager"
                        || role.RoleName == "Owner"
                        || role.RoleName == "Admin")
                select new CashierCredential(
                    user.Id,
                    user.Username,
                    user.PinHash,
                    user.PasswordHash,
                    role.RoleName))
                .ToListAsync(ct);

            CashierCredential? matched = candidates.FirstOrDefault(candidate =>
                CryptoHelper.VerifyPassword(password, candidate.PasswordHash));

            return matched is null ? null : ToDto(matched);
        },
        cancellationToken);
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

        return matched.RoleName is "Manager" or "Owner" or "Admin";
    }

    private static CashierDto ToDto(CashierCredential cashier) =>
        new()
        {
            CashierId = cashier.Id,
            Name = cashier.Name,
            RoleName = cashier.RoleName
        };

    private sealed record CashierCredential(
        Guid Id,
        string Name,
        string PinHash,
        string PasswordHash,
        string RoleName);
}
