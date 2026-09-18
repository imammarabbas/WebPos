using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Data;
using WebPos.Core.Entities;
using WebPos.Core.Models;
using WebPos.Core.Services;

namespace WebPos.Core.Security;

public sealed class EnrollmentService(
    IDbContextFactory<WebPosDbContext> dbFactory,
    IKeyProvider keyProvider,
    IConfiguration configuration,
    ILogger<EnrollmentService> logger)
{
    private const int MinimumRsaKeySize = 3072;
    private const int DefaultLifetimeDays = 365;

    private readonly IDbContextFactory<WebPosDbContext> _dbFactory =
        dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
    private readonly IKeyProvider _keyProvider =
        keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
    private readonly IConfiguration _configuration =
        configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<EnrollmentService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<(EnrollmentCertificateDto? Certificate, string? FailureReason)> TryEnrollAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string username = request.AdminUsername?.Trim() ?? string.Empty;
        if (request.TerminalId == Guid.Empty
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return Task.FromResult(Failure(
                "Terminal id, admin username, and password are required."));
        }

        return DbContextExecution.ExecuteAsync(
            _dbFactory,
            async (context, ct) =>
            {
            Terminal? terminal = await context.Terminals
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(
                    candidate => candidate.Id == request.TerminalId,
                    ct);
            if (terminal is null || terminal.TenantId == Guid.Empty)
            {
                return Failure(
                    $"Unknown terminal id '{request.TerminalId}'. " +
                    "Confirm Docker API finished seeding and the Terminal ID matches the database.");
            }

            if (!terminal.IsActive)
            {
                if (!AllowDevRepair())
                {
                    return Failure($"Terminal '{request.TerminalId}' is inactive.");
                }

                terminal.IsActive = true;
                _logger.LogWarning(
                    "Reactivated inactive terminal {TerminalId} for enrollment.",
                    terminal.Id);
            }

            // Multiple "admin" rows can exist after a DB restore. Match by password,
            // then prefer terminal tenant → master tenant → any.
            List<User> adminMatches = await context.Users
                .IgnoreQueryFilters()
                .Include(user => user.Role)
                .Where(user => user.IsActive && user.Username.ToLower() == username.ToLower())
                .ToListAsync(ct);

            if (adminMatches.Count == 0)
            {
                _logger.LogWarning(
                    "Admin user '{Username}' not found in terminal tenant {TenantId} or master tenant {MasterTenantId}.",
                    username,
                    terminal.TenantId,
                    TenantDefaults.MasterTenantId);
                return Failure(
                    $"No active user '{username}'. " +
                    "Rebuild API (`docker compose up --build`) so PilotDataSeeder seeds admin.");
            }

            List<User> passwordMatches = adminMatches
                .Where(u => CryptoHelper.VerifyPassword(request.AdminPassword, u.PasswordHash))
                .ToList();

            if (passwordMatches.Count == 0)
            {
                _logger.LogWarning(
                    "Password verification failed for admin user '{Username}' ({CandidateCount} candidate row(s)).",
                    username,
                    adminMatches.Count);
                return Failure(
                    "Admin password incorrect. Use the exact same password that works on Master portal " +
                    "(pilot default: admin / admin123).");
            }

            List<User> roleMatches = passwordMatches
                .Where(u => IsMasterPortalRole(u.Role?.RoleName))
                .ToList();

            if (roleMatches.Count == 0)
            {
                _logger.LogWarning(
                    "User '{Username}' password matched but none of {Count} row(s) have Owner/Manager role.",
                    username,
                    passwordMatches.Count);
                return Failure(
                    $"User '{username}' authenticated but is not Owner/Manager — " +
                    "those roles are required for enrollment.");
            }

            User administrator =
                roleMatches.FirstOrDefault(u => u.TenantId == terminal.TenantId)
                ?? roleMatches.FirstOrDefault(u => u.TenantId == TenantDefaults.MasterTenantId)
                ?? roleMatches[0];

            if (administrator.TenantId != terminal.TenantId)
            {
                if (!AllowDevRepair())
                {
                    return Failure(
                        $"User '{username}' store ({administrator.TenantId}) does not match terminal store " +
                        $"({terminal.TenantId}).");
                }

                _logger.LogWarning(
                    "Rebinding terminal {TerminalId} from tenant {OldTenantId} to admin user '{Username}' tenant {NewTenantId}.",
                    terminal.Id,
                    terminal.TenantId,
                    username,
                    administrator.TenantId);
                terminal.TenantId = administrator.TenantId;
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(ct);
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

            // Export parameters so signing does not depend on the disposable RSA instance
            // (avoids ObjectDisposedException / RSAOpenSsl under Linux Docker).
            RsaSecurityKey signingKey = new(rsa.ExportParameters(includePrivateParameters: true));

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
                    signingKey,
                    SecurityAlgorithms.RsaSha256));
            string serializedToken =
                new JwtSecurityTokenHandler().WriteToken(token);

            return (
                new EnrollmentCertificateDto
                {
                    Token = serializedToken,
                    TenantId = terminal.TenantId,
                    TerminalId = terminal.Id,
                    ExpiresAtUtc = expiresAt
                },
                (string?)null);
        },
        cancellationToken);
    }

    private static (EnrollmentCertificateDto? Certificate, string? FailureReason) Failure(
        string reason) =>
        (null, reason);

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
