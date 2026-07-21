using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebPos.Core.Abstractions;
using WebPos.Core.Data;
using WebPos.Core.Interfaces;
using WebPos.Core.Models;

namespace WebPos.Core.Security;

/// <summary>
/// Issues tenant-scoped user JWTs. Tenant context comes from the enrollment
/// certificate on the login request; the token then carries it as the tid claim.
/// </summary>
public sealed class JwtService : ISecurityService
{
    public const string TenantClaimType = "tid";
    public const string RoleClaimType = "role";

    private readonly WebPosDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly JwtSigningKey _signingKey;
    private readonly JwtOptions _options;

    public JwtService(
        WebPosDbContext context,
        ITenantService tenantService,
        JwtSigningKey signingKey,
        IOptions<JwtOptions> options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantService = tenantService
            ?? throw new ArgumentNullException(nameof(tenantService));
        _signingKey = signingKey ?? throw new ArgumentNullException(nameof(signingKey));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public async Task<AuthResponse?> AuthenticateAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_tenantService.IsResolved || _tenantService.TenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required to authenticate users.");
        }

        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        Guid tenantId = _tenantService.TenantId;
        User? user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(
                u => u.TenantId == tenantId
                     && u.Username == request.Username
                     && u.IsActive,
                cancellationToken);

        if (user is null || !CryptoHelper.VerifyPassword(request.Password, user.PasswordHash))
        {
            return null;
        }

        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(TenantClaimType, user.TenantId.ToString()),
            new(RoleClaimType, user.Role.RoleName),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        ];

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(
                _signingKey.Key,
                SecurityAlgorithms.HmacSha256));

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAtUtc = expiresAt,
            Username = user.Username,
            Role = user.Role.RoleName
        };
    }
}
