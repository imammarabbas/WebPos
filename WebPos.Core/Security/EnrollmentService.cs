using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;

namespace WebPos.Core.Security;

public sealed class EnrollmentService(
    WebPosDbContext context,
    IKeyProvider keyProvider,
    IConfiguration configuration)
{
    private const int MinimumRsaKeySize = 3072;
    private const int DefaultLifetimeDays = 365;

    private readonly WebPosDbContext _context =
        context ?? throw new ArgumentNullException(nameof(context));
    private readonly IKeyProvider _keyProvider =
        keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly IConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));

    public async Task<(EnrollmentCertificateDto? Certificate, string? FailureReason)> TryEnrollAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string username = request.AdminUsername?.Trim() ?? string.Empty;
        if (request.TerminalId == Guid.Empty
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return (null, "Terminal id, admin username, and password are required.");
        }

        Terminal? terminal = await _context.Terminals
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.TerminalId,
                cancellationToken);
        if (terminal is null || terminal.TenantId == Guid.Empty)
        {
            return (null,
                $"Unknown terminal id '{request.TerminalId}'. " +
                "Confirm Docker API finished seeding and the Terminal ID matches the database.");
        }

        if (!terminal.IsActive)
        {
            if (!AllowDevRepair())
            {
                return (null, $"Terminal '{request.TerminalId}' is inactive.");
            }

            terminal.IsActive = true;
        }

        // Prefer user on this terminal's tenant (Master login ignores tenant and can pick another row).
        List<User> adminMatches = await _context.Users
            .IgnoreQueryFilters()
            .Include(user => user.Role)
            .Where(user => user.IsActive && user.Username.ToLower() == username.ToLower())
            .ToListAsync(cancellationToken);

        User? administrator =
            adminMatches.FirstOrDefault(u => u.TenantId == terminal.TenantId)
            ?? adminMatches.FirstOrDefault(u => u.TenantId == TenantDefaults.MasterTenantId)
            ?? adminMatches.FirstOrDefault();

        if (administrator is null)
        {
            return (null,
                $"No active user '{username}'. " +
                "Rebuild API (`docker compose up --build`) so PilotDataSeeder seeds admin.");
        }

        if (!CryptoHelper.VerifyPassword(request.AdminPassword, administrator.PasswordHash))
        {
            return (null,
                "Admin password incorrect. Use the exact same password that works on Master portal " +
                "(pilot default: admin / admin123).");
        }

        if (!IsMasterPortalRole(administrator.Role?.RoleName))
        {
            return (null,
                $"User '{username}' role is '{administrator.Role?.RoleName ?? "(none)"}' — " +
                "Owner/Manager required for enrollment.");
        }

        if (administrator.TenantId != terminal.TenantId)
        {
            if (!AllowDevRepair())
            {
                return (null,
                    $"User '{username}' store ({administrator.TenantId}) does not match terminal store " +
                    $"({terminal.TenantId}).");
            }

            // Common after restoring backup.sql onto a new machine / Docker volume.
            terminal.TenantId = administrator.TenantId;
        }

        if (_context.ChangeTracker.HasChanges())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        int lifetimeDays = _configuration.GetValue(
            "Security:Enrollment:LifetimeDays",
            DefaultLifetimeDays);
        DateTimeOffset expiresAt =
            issuedAt.AddDays(Math.Clamp(lifetimeDays, 1, DefaultLifetimeDays));

        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(_keyProvider.GetPrivateKeyPem());
        if (rsa.KeySize < MinimumRsaKeySize)
        {
            throw new InvalidOperationException(
                $"Enrollment signing key must be at least {MinimumRsaKeySize} bits. " +
                "Delete WebPos/dev-secrets/ and restart the API, or run scripts/Generate-PilotEnv.ps1.");
        }

        Claim[] claims =
        [
            new(
                EnrollmentCertificateValidator.TenantClaimType,
                terminal.TenantId.ToString()),
            new(
                EnrollmentCertificateValidator.TerminalClaimType,
                terminal.Id.ToString()),
            new(
                EnrollmentCertificateValidator.CertificateTypeClaim,
                EnrollmentCertificateValidator.EnrollmentCertificateType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];
        var token = new JwtSecurityToken(
            issuer: EnrollmentCertificateValidator.Issuer,
            audience: EnrollmentCertificateValidator.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256));
        string serializedToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        return (new EnrollmentCertificateDto
        {
            Token = serializedToken,
            TenantId = terminal.TenantId,
            TerminalId = terminal.Id,
            ExpiresAtUtc = expiresAt
        }, null);
    }

    public async Task<EnrollmentCertificateDto?> EnrollAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        (EnrollmentCertificateDto? certificate, _) =
            await TryEnrollAsync(request, cancellationToken);
        return certificate;
    }

    private bool AllowDevRepair() =>
        _configuration.GetValue("Security:AllowInsecureDevDefaults", false)
        || string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsMasterPortalRole(string? roleName) =>
        string.Equals(roleName, "Owner", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
}
