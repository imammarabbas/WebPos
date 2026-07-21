using System.Security.Claims;
using WebPos.Client.Sdk.Security;
using WebPos.Core.Interfaces;

namespace WebPos.Core.Services;

public sealed class HttpContextTenantService(IHttpContextAccessor httpContextAccessor)
    : ITenantService
{
    public const string TenantClaimType = "tenant_id";
    public const string JwtTenantClaimType = "tid";

    private readonly IHttpContextAccessor _httpContextAccessor =
        httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

    public Guid TenantId =>
        TryResolveTenantId(out Guid tenantId) ? tenantId : Guid.Empty;

    public bool IsResolved => TryResolveTenantId(out _);

    private bool TryResolveTenantId(out Guid tenantId)
    {
        ClaimsIdentity? identity = _httpContextAccessor.HttpContext?.User.Identities
            .FirstOrDefault(candidate =>
                candidate.IsAuthenticated
                && string.Equals(
                    candidate.AuthenticationType,
                        EnrollmentCertificateValidator.AuthenticationType,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.FindFirst(
                        EnrollmentCertificateValidator.CertificateTypeClaim)?.Value,
                    EnrollmentCertificateValidator.EnrollmentCertificateType,
                    StringComparison.Ordinal));
        Claim? claim = identity?.FindFirst(TenantClaimType);
        if (Guid.TryParse(claim?.Value, out tenantId) && tenantId != Guid.Empty)
        {
            return true;
        }

        // Fallback: tid claim from a validated user JWT (JwtBearer identity).
        Claim? jwtClaim = _httpContextAccessor.HttpContext?.User.Identities
            .Where(candidate => candidate.IsAuthenticated)
            .Select(candidate => candidate.FindFirst(JwtTenantClaimType))
            .FirstOrDefault(candidate => candidate is not null);
        return Guid.TryParse(jwtClaim?.Value, out tenantId)
            && tenantId != Guid.Empty;
    }
}
