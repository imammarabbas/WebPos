using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Data;
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

    public async Task<EnrollmentCertificateDto?> EnrollAsync(
        EnrollTerminalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TerminalId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.AdminUsername)
            || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return null;
        }

        Terminal? terminal = await _context.Terminals
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Id == request.TerminalId
                    && candidate.IsActive,
                cancellationToken);
        if (terminal is null || terminal.TenantId == Guid.Empty)
        {
            return null;
        }

        User? administrator = await _context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(user => user.Role)
            .SingleOrDefaultAsync(
                user =>
                    user.TenantId == terminal.TenantId
                    && user.Username == request.AdminUsername
                    && user.IsActive,
                cancellationToken);
        if (administrator is null
            || !IsMasterPortalRole(administrator.Role.RoleName)
            || !CryptoHelper.VerifyPassword(
                request.AdminPassword,
                administrator.PasswordHash))
        {
            return null;
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
                $"Enrollment signing key must be at least {MinimumRsaKeySize} bits.");
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

        return new EnrollmentCertificateDto
        {
            Token = serializedToken,
            TenantId = terminal.TenantId,
            TerminalId = terminal.Id,
            ExpiresAtUtc = expiresAt
        };
    }

    private static bool IsMasterPortalRole(string? roleName) =>
        string.Equals(roleName, "Owner", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Manager", StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
}
